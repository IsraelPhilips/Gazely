"""Offline Nemotron streaming via pinned NeMo-Speech 0.2.0 on loopback only.
One cache-aware stream per session. Length-prefixed PCM16/16k input; zero flushes.
No recording, external service, downloads, prompts or vocabulary replacements.
The local server exposes thread control missing from this release's C ABI.
"""
import base64
import ctypes as C
from ctypes import wintypes as W
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
import websocket

output_lock = threading.Lock()


def emit(*parts):
    with output_lock:
        print('\t'.join(map(str, parts)), flush=True)


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
    process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=sys.stderr,
                               stderr=sys.stderr, creationflags=subprocess.CREATE_NO_WINDOW)
    job, ws = None, None
    completed, progress = threading.Event(), threading.Event()
    errors, received = [], [0]
    try:
        job = ChildJob(process)
        emit('WORKER', os.getpid())
        emit('SERVER', process.pid)
        deadline = time.monotonic() + 45
        while True:
            if process.poll() is not None:
                raise RuntimeError('Local Nemotron server exited during startup')
            try:
                ws = websocket.create_connection(
                    f'ws://127.0.0.1:{port}/v1/audio/transcriptions/realtime', timeout=1,
                    header={'Authorization': 'Bearer ' + token}, http_no_proxy=['127.0.0.1'])
                break
            except (OSError, websocket.WebSocketException):
                if time.monotonic() > deadline:
                    raise TimeoutError('Local Nemotron server startup timed out')
                time.sleep(.1)
        ws.settimeout(35)
        if json.loads(ws.recv())['type'] != 'session.created':
            raise RuntimeError('Unexpected streaming handshake')
        ws.send(json.dumps({'type': 'session.update', 'session': {
            'sample_rate': 16000, 'language': 'en', 'automatic_punctuation': False,
            'endpointing_ms': 700, 'word_timestamps': False}}))
        if json.loads(ws.recv())['type'] != 'session.updated':
            raise RuntimeError('Streaming configuration rejected')

        def receive():
            utterance, start, partial, last = 1, 0, '', None
            try:
                while not completed.is_set():
                    opcode, payload = ws.recv_data(control_frame=True)
                    if opcode == websocket.ABNF.OPCODE_PONG:
                        # Ordered ping follows each audio frame: it acknowledges
                        # decoding even in silence, when ASR emits no text event.
                        acknowledged, sent_at = payload.decode('ascii').split(':')
                        received[0] = max(received[0], int(acknowledged))
                        emit('PROGRESS', received[0], round((time.perf_counter()-float(sent_at))*1000, 3))
                        progress.set()
                        continue
                    if opcode == websocket.ABNF.OPCODE_CLOSE:
                        raise EOFError('Local speech server closed before flush')
                    if opcode != websocket.ABNF.OPCODE_TEXT:
                        continue
                    event = json.loads(payload)
                    kind = event['type']
                    if kind == 'error':
                        raise RuntimeError(event['error']['message'])
                    if kind == 'input_audio_buffer.committed':
                        completed.set()
                        return
                    if kind.endswith('.delta') or kind.endswith('.completed'):
                        final = kind.endswith('.completed')
                        end = max(start, round(event['audio_processed'] * 16000))
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
                completed.set()
                progress.set()

        reader = threading.Thread(target=receive, daemon=True)
        reader.start()
        emit('READY')
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
            ws.send_binary(pcm)
            fed += count // 2
            ws.ping(str(fed)+':'+str(time.perf_counter()))
        ws.send(json.dumps({'type': 'input_audio_buffer.commit'}))
        if not completed.wait(25):
            raise TimeoutError('Nemotron did not confirm final audio flush')
        if errors:
            raise errors[0]
        emit('DONE')
    finally:
        completed.set()
        if ws:
            ws.close(timeout=1)
        if process.poll() is None:
            process.terminate()
        process.wait(timeout=5)
        if job:
            job.close()


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        emit('ERROR', base64.b64encode((type(exc).__name__ + ': ' + str(exc)).encode()).decode())
        raise
