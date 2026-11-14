"""Offline Nemotron streaming via pinned NeMo-Speech 0.2.0 on loopback only.
One cache-aware stream per session. Length-prefixed PCM16/16k input; zero flushes.
No recording, external service, downloads, prompts or vocabulary replacements.
The local server exposes thread control missing from this release's C ABI.
"""
import base64
import ctypes as C
from ctypes import wintypes as W
import datetime
import hashlib
import json
import os
import pathlib
import secrets
import socket
import struct
import subprocess
import sys
import threading
import time
import traceback
import websocket

output_lock = threading.Lock()


def emit(*parts):
    with output_lock:
        print('\t'.join(map(str, parts)), flush=True)


def diagnostic(session_id, event, **fields):
    record = {
        'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='milliseconds'),
        'monotonic_s': round(time.monotonic(), 3),
        'session': session_id,
        'event': event,
    }
    record.update(fields)
    payload = json.dumps(record, ensure_ascii=False, separators=(',', ':'), default=str)
    emit('DIAG', base64.b64encode(payload.encode('utf-8')).decode('ascii'))


def drain_native_output(stream, session_id, process_id, channel):
    try:
        for line in iter(stream.readline, ''):
            diagnostic(session_id, 'native_output', pid=process_id, channel=channel,
                       text=line.rstrip('\r\n'))
    except Exception:
        diagnostic(session_id, 'native_output_drain_error', pid=process_id,
                   channel=channel, traceback=traceback.format_exc())
    finally:
        diagnostic(session_id, 'native_output_eof', pid=process_id, channel=channel)


def exact(stream, count):
    data = bytearray()
    while len(data) < count:
        block = stream.read(count - len(data))
        if not block:
            raise EOFError('Truncated PCM packet; stream was not flushed')
        data.extend(block)
    return data


class ChildJob:
    """Closing the worker closes this kernel handle and kills its local server."""
    class Basic(C.Structure):
        _fields_ = [('process_time', C.c_int64), ('job_time', C.c_int64),
                    ('flags', W.DWORD), ('min_working', C.c_size_t),
                    ('max_working', C.c_size_t), ('active_limit', W.DWORD),
                    ('affinity', C.c_size_t), ('priority', W.DWORD), ('scheduling', W.DWORD)]

    class IO(C.Structure):
        _fields_ = [(name, C.c_uint64) for name in
                    ('read_ops', 'write_ops', 'other_ops', 'read_bytes', 'write_bytes', 'other_bytes')]

    def __init__(self, process):
        class Extended(C.Structure):
            _fields_ = [('basic', ChildJob.Basic), ('io', ChildJob.IO)] + [
                (name, C.c_size_t) for name in ('process_memory', 'job_memory', 'peak_process', 'peak_job')]
        self.kernel = C.WinDLL('kernel32', use_last_error=True)
        self.kernel.CreateJobObjectW.argtypes = [C.c_void_p, W.LPCWSTR]
        self.kernel.CreateJobObjectW.restype = W.HANDLE
        self.kernel.SetInformationJobObject.argtypes = [W.HANDLE, C.c_int, C.c_void_p, W.DWORD]
        self.kernel.AssignProcessToJobObject.argtypes = [W.HANDLE, W.HANDLE]
        self.kernel.CloseHandle.argtypes = [W.HANDLE]
        self.handle = self.kernel.CreateJobObjectW(None, None)
        limits = Extended()
        limits.basic.flags = 0x2000
        if not self.handle or not self.kernel.SetInformationJobObject(
                self.handle, 9, C.byref(limits), C.sizeof(limits)) or not self.kernel.AssignProcessToJobObject(
                    self.handle, W.HANDLE(int(process._handle))):
            code = C.get_last_error()
            self.close()
            raise C.WinError(code)

    def close(self):
        if self.handle:
            self.kernel.CloseHandle(self.handle)
            self.handle = None


def main():
    session_id = sys.argv[3] if len(sys.argv) > 3 else secrets.token_hex(12)
    diagnostic(session_id, 'worker_start', worker_pid=os.getpid(), argv_count=len(sys.argv))
    assets = pathlib.Path(sys.argv[1]).resolve()
    lock = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
    entry = next(x for x in lock if x['path'].endswith('.gguf'))
    model = assets / entry['path']
    with model.open('rb') as f:
        digest = hashlib.file_digest(f, 'sha256').hexdigest()
    if model.stat().st_size != entry['bytes'] or digest != entry['sha256']:
        raise ValueError('Nemotron model integrity mismatch')
    runtime = assets / 'nemo-runtime/nemo-speech-0.2.0-windows-x86_64-cpu/bin/nemo-speech.exe'
    with socket.socket() as reservation:
        reservation.bind(('127.0.0.1', 0))
        port = reservation.getsockname()[1]
    token = secrets.token_hex(24)
    command = [str(runtime), 'serve', '--asr-model', str(model), '--device', 'cpu',
               '--asr.backend.threads', '4', '--asr.streaming.rnnt_right_context', '6',
               '--asr.endpointing.enable', '--host', '127.0.0.1', '--port', str(port),
               '--api-key', token, '--no-ui', '--read-timeout', '60', '--max-upload-mb', '4096']
    process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True, encoding='utf-8', errors='replace',
                               bufsize=1, creationflags=subprocess.CREATE_NO_WINDOW)
    output_threads = [
        threading.Thread(target=drain_native_output, args=(process.stdout, session_id, process.pid, 'stdout'), daemon=True),
        threading.Thread(target=drain_native_output, args=(process.stderr, session_id, process.pid, 'stderr'), daemon=True),
    ]
    for thread in output_threads:
        thread.start()
    native_exit_seen = threading.Event()

    def monitor_native_exit():
        exit_code = process.wait()
        native_exit_seen.set()
        diagnostic(session_id, 'native_process_exit', pid=process.pid, exit_code=exit_code,
                   expected=intentional_native_stop.is_set())

    intentional_native_stop = threading.Event()
    monitor = threading.Thread(target=monitor_native_exit, daemon=True)
    monitor.start()
    job, ws = None, None
    completed, progress = threading.Event(), threading.Event()
    errors, received = [], [0]
    sent_frames, sent_samples = [0], [0]
    first_audio_sent_at, last_ack_roundtrip_ms = [None], [None]
    responses = {'partial': 0, 'final': 0}
    try:
        job = ChildJob(process)
        emit('WORKER', os.getpid())
        emit('SERVER', process.pid)
        diagnostic(session_id, 'native_process_started', worker_pid=os.getpid(), native_pid=process.pid,
                   executable=str(runtime), model=str(model))
        deadline = time.monotonic() + 45
        connect_attempts = 0
        while True:
            if process.poll() is not None:
                raise RuntimeError('Local Nemotron server exited during startup')
            connect_attempts += 1
            try:
                if connect_attempts == 1:
                    diagnostic(session_id, 'websocket_connecting', native_pid=process.pid,
                               host='127.0.0.1', port=port, path='/v1/audio/transcriptions/realtime')
                ws = websocket.create_connection(
                    f'ws://127.0.0.1:{port}/v1/audio/transcriptions/realtime', timeout=1,
                    header={'Authorization': 'Bearer ' + token}, http_no_proxy=['127.0.0.1'])
                diagnostic(session_id, 'websocket_connected', native_pid=process.pid,
                           attempt=connect_attempts, host='127.0.0.1', port=port)
                break
            except (OSError, websocket.WebSocketException) as exc:
                if time.monotonic() > deadline:
                    diagnostic(session_id, 'websocket_connect_timeout', attempts=connect_attempts,
                               exception_type=type(exc).__name__, exception=str(exc),
                               traceback=traceback.format_exc())
                    raise TimeoutError('Local Nemotron server startup timed out')
                if connect_attempts == 1 or connect_attempts % 25 == 0:
                    diagnostic(session_id, 'websocket_connect_retry', attempt=connect_attempts,
                               exception_type=type(exc).__name__, exception=str(exc))
                time.sleep(.1)
        ws.settimeout(35)
        created = json.loads(ws.recv())
        diagnostic(session_id, 'session_created_received', event_type=created.get('type'), native_pid=process.pid)
        if created.get('type') != 'session.created':
            raise RuntimeError('Unexpected streaming handshake')
        ws.send(json.dumps({'type': 'session.update', 'session': {
            'sample_rate': 16000, 'language': 'en', 'automatic_punctuation': False,
            'endpointing_ms': 700, 'word_timestamps': False}}))
        diagnostic(session_id, 'session_update_sent', sample_rate=16000, language='en')
        updated = json.loads(ws.recv())
        diagnostic(session_id, 'session_updated_received', event_type=updated.get('type'))
        if updated.get('type') != 'session.updated':
            raise RuntimeError('Streaming configuration rejected')

        def receive():
            utterance, start, partial, last = 1, 0, '', None
            try:
                while not completed.is_set():
                    opcode, payload = ws.recv_data(control_frame=True)
                    if opcode == websocket.ABNF.OPCODE_PONG:
                        # Ordered ping follows each audio frame: it acknowledges
                        # decoding even in silence, when ASR emits no text event.
                        echoed_samples, sent_at = payload.decode('ascii').split(':')
                        received[0] = max(received[0], int(echoed_samples))
                        last_ack_roundtrip_ms[0] = round((time.perf_counter()-float(sent_at))*1000, 3)
                        # The protocol proves the peer echoed our ping payload;
                        # without native server source we cannot claim this is
                        # an inference-completion acknowledgement.
                        emit('PROGRESS', received[0], last_ack_roundtrip_ms[0])
                        progress.set()
                        continue
                    if opcode == websocket.ABNF.OPCODE_CLOSE:
                        close_code = struct.unpack('!H', payload[:2])[0] if len(payload) >= 2 else None
                        close_reason = payload[2:].decode('utf-8', errors='replace') if len(payload) > 2 else ''
                        diagnostic(session_id, 'websocket_close_frame', code=close_code, reason=close_reason,
                                   native_pid=process.pid, received_samples=received[0])
                        raise EOFError('Local speech server closed before flush')
                    if opcode != websocket.ABNF.OPCODE_TEXT:
                        continue
                    event = json.loads(payload)
                    kind = event['type']
                    if kind == 'error':
                        diagnostic(session_id, 'server_error_event', native_pid=process.pid,
                                   error=event.get('error'))
                        raise RuntimeError(event['error']['message'])
                    if kind == 'input_audio_buffer.committed':
                        diagnostic(session_id, 'session_committed_received', native_pid=process.pid,
                                   sent_samples=sent_samples[0], pong_echoed_samples=received[0])
                        completed.set()
                        return
                    if kind.endswith('.delta') or kind.endswith('.completed'):
                        final = kind.endswith('.completed')
                        responses['final' if final else 'partial'] += 1
                        end = max(start, round(event['audio_processed'] * 16000))
                        response_delay_ms = None
                        if first_audio_sent_at[0] is not None:
                            response_delay_ms = round(max(0, time.monotonic() -
                                (first_audio_sent_at[0] + end / 16000.0)) * 1000, 1)
                        diagnostic(session_id, 'transcription_response', kind='final' if final else 'partial',
                                   response_count=responses['final' if final else 'partial'],
                                   audio_start_sample=start, audio_end_sample=end,
                                   audio_processed_s=round(end / 16000.0, 3),
                                   client_response_delay_ms=response_delay_ms,
                                   latest_ack_roundtrip_ms=last_ack_roundtrip_ms[0],
                                   transcript_chars=len(event.get('transcript', '')) if final else None)
                        if final:
                            value = event['transcript']
                        else:
                            partial += event['delta']
                            value = partial
                        if final or value != last:
                            emit('FINAL' if final else 'PARTIAL', utterance, start, end,
                                 base64.b64encode(value.strip().encode()).decode())
                            last = value
                        if final:
                            utterance, start, partial, last = utterance + 1, end, '', None
            except Exception as exc:
                errors.append(exc)
                diagnostic(session_id, 'receiver_exception', exception_type=type(exc).__name__,
                           exception=str(exc), winerror=getattr(exc, 'winerror', None),
                           errno=getattr(exc, 'errno', None), native_pid=process.pid,
                           received_samples=received[0], traceback=traceback.format_exc())
                completed.set()
                progress.set()

        reader = threading.Thread(target=receive, daemon=True)
        reader.start()
        emit('READY')
        diagnostic(session_id, 'stream_ready', native_pid=process.pid, worker_pid=os.getpid())
        fed = 0
        while True:
            count = struct.unpack('<I', exact(sys.stdin.buffer, 4))[0]
            if not count:
                break
            if count > 32000 or count % 2:
                raise ValueError('Invalid PCM16 packet')
            pcm = exact(sys.stdin.buffer, count)
            # Bound hidden transport buffers; C# owns the main bounded queue.
            deadline = time.monotonic() + 20
            while fed - received[0] > 48000:
                if errors:
                    raise errors[0]
                if time.monotonic() > deadline:
                    raise TimeoutError('Nemotron made no streaming progress for 20 seconds')
                progress.wait(.1)
                progress.clear()
            if errors:
                raise errors[0]
            if first_audio_sent_at[0] is None:
                first_audio_sent_at[0] = time.monotonic()
            ws.send_binary(pcm)
            fed += count // 2
            sent_samples[0] = fed
            sent_frames[0] += 1
            ws.ping(str(fed)+':'+str(time.perf_counter()))
            if sent_frames[0] % 50 == 0:
                diagnostic(session_id, 'audio_progress', native_pid=process.pid,
                           sent_frames=sent_frames[0], sent_samples=fed,
                           pong_echoed_samples=received[0],
                           unconfirmed_audio_ms=round(max(0, fed-received[0]) / 16.0, 1),
                           partial_responses=responses['partial'], final_responses=responses['final'])
        ws.send(json.dumps({'type': 'input_audio_buffer.commit'}))
        diagnostic(session_id, 'flush_sent', native_pid=process.pid, sent_frames=sent_frames[0],
                   sent_samples=fed, pong_echoed_samples=received[0])
        if not completed.wait(25):
            diagnostic(session_id, 'flush_timeout', native_pid=process.pid, sent_samples=fed,
                       pong_echoed_samples=received[0], traceback='No commit confirmation within 25 seconds')
            raise TimeoutError('Nemotron did not confirm final audio flush')
        if errors:
            raise errors[0]
        emit('DONE')
        diagnostic(session_id, 'stream_done', native_pid=process.pid, sent_samples=fed,
                   pong_echoed_samples=received[0], partial_responses=responses['partial'],
                   final_responses=responses['final'])
    except Exception as exc:
        # Preserve the native exit status before cleanup; a lost socket alone
        # cannot distinguish a server crash from a transport interruption.
        time.sleep(.1)
        native_exit = process.poll()
        diagnostic(session_id, 'worker_exception', exception_type=type(exc).__name__,
                   exception=str(exc), winerror=getattr(exc, 'winerror', None),
                   errno=getattr(exc, 'errno', None), native_pid=process.pid,
                   native_exit_code=native_exit, received_samples=received[0],
                   sent_samples=sent_samples[0], sent_frames=sent_frames[0],
                   partial_responses=responses['partial'], final_responses=responses['final'],
                   traceback=traceback.format_exc())
        raise RuntimeError(f'{type(exc).__name__}: {exc}; native_exit={native_exit}; '
                           f'audio_received={received[0] / 16000:.3f}s') from exc
    finally:
        diagnostic(session_id, 'worker_cleanup_started', native_pid=process.pid,
                   native_exit_code=process.poll(), completed=completed.is_set())
        completed.set()
        if ws:
            try:
                ws.close(timeout=1)
                diagnostic(session_id, 'websocket_closed_locally', native_pid=process.pid)
            except Exception:
                diagnostic(session_id, 'websocket_close_exception', native_pid=process.pid,
                           traceback=traceback.format_exc())
        if process.poll() is None:
            intentional_native_stop.set()
            process.terminate()
        process.wait(timeout=5)
        monitor.join(timeout=1)
        for thread in output_threads:
            thread.join(timeout=1)
        diagnostic(session_id, 'worker_cleanup_finished', native_pid=process.pid,
                   native_exit_code=process.poll(), native_exit_observed=native_exit_seen.is_set())
        if job:
            job.close()


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        # Preserve a serialized full traceback for the desktop diagnostics file.
        diagnostic(sys.argv[3] if len(sys.argv) > 3 else 'unknown', 'top_level_exception',
                   exception_type=type(exc).__name__, exception=str(exc),
                   winerror=getattr(exc, 'winerror', None), errno=getattr(exc, 'errno', None),
                   traceback=traceback.format_exc())
        emit('ERROR', base64.b64encode((type(exc).__name__ + ': ' + str(exc)).encode()).decode())
        raise
