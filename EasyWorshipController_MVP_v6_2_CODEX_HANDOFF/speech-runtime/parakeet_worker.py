"""Local cache-aware Parakeet EOU streaming bridge; stdout carries the C# protocol."""
import array
import base64
import ctypes as C
import datetime
import hashlib
import json
import os
import pathlib
import struct
import sys
import time
import traceback


def emit(*parts):
    print('\t'.join(map(str, parts)), flush=True)


def note(session, event, **fields):
    record = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='milliseconds'),
              'monotonic_s': round(time.monotonic(), 3), 'session': session, 'event': event}
    record.update(fields)
    print('Parakeet: ' + json.dumps(record, ensure_ascii=False, separators=(',', ':'), default=str),
          file=sys.stderr, flush=True)


def exact(stream, count):
    data = bytearray()
    while len(data) < count:
        part = stream.read(count - len(data))
        if not part:
            raise EOFError('Truncated audio packet; stream was not flushed')
        data.extend(part)
    return data


def main():
    session = sys.argv[3] if len(sys.argv) > 3 else 'untracked'
    package = pathlib.Path(sys.argv[1]).resolve()
    manifest = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
    model_entry = manifest['model']
    model = package / model_entry['path']
    with model.open('rb') as source:
        digest = hashlib.file_digest(source, 'sha256').hexdigest()
    if model.stat().st_size != model_entry['bytes'] or digest != model_entry['sha256']:
        raise ValueError('Parakeet model integrity mismatch')

    dlls = list((package / 'runtime').rglob('parakeet.dll'))
    if len(dlls) != 1:
        raise FileNotFoundError('Expected one installed Parakeet CPU runtime DLL')
    dll_directory = os.add_dll_directory(str(dlls[0].parent))
    lib = C.CDLL(str(dlls[0]))
    lib.parakeet_capi_abi_version.restype = C.c_int
    lib.parakeet_capi_load.argtypes = [C.c_char_p]
    lib.parakeet_capi_load.restype = C.c_void_p
    lib.parakeet_capi_load_error.restype = C.c_char_p
    lib.parakeet_capi_free.argtypes = [C.c_void_p]
    lib.parakeet_capi_stream_begin.argtypes = [C.c_void_p]
    lib.parakeet_capi_stream_begin.restype = C.c_void_p
    lib.parakeet_capi_stream_feed.argtypes = [C.c_void_p, C.POINTER(C.c_float), C.c_int,
                                              C.POINTER(C.c_int)]
    lib.parakeet_capi_stream_feed.restype = C.c_void_p
    lib.parakeet_capi_stream_finalize.argtypes = [C.c_void_p]
    lib.parakeet_capi_stream_finalize.restype = C.c_void_p
    lib.parakeet_capi_stream_free.argtypes = [C.c_void_p]
    lib.parakeet_capi_free_string.argtypes = [C.c_void_p]
    lib.parakeet_capi_last_error.argtypes = [C.c_void_p]
    lib.parakeet_capi_last_error.restype = C.c_char_p

    required = ('parakeet_capi_abi_version', 'parakeet_capi_load', 'parakeet_capi_free',
                'parakeet_capi_stream_begin', 'parakeet_capi_stream_feed',
                'parakeet_capi_stream_finalize', 'parakeet_capi_stream_free',
                'parakeet_capi_free_string', 'parakeet_capi_last_error')
    for symbol in required:
        if not hasattr(lib, symbol):
            raise RuntimeError('Parakeet runtime is missing C API symbol: ' + symbol)

    ctx = None
    stream = None
    try:
        abi = lib.parakeet_capi_abi_version()
        if abi < 5:
            raise RuntimeError('Parakeet C API ABI is too old for EOU streaming: ' + str(abi))
        note(session, 'runtime_loaded', abi=abi, pid=os.getpid(), model_bytes=model.stat().st_size)
        ctx = lib.parakeet_capi_load(os.fsencode(model))
        if not ctx:
            reason = lib.parakeet_capi_load_error()
            raise RuntimeError('Model load failed: ' + (reason.decode('utf-8', 'replace') if reason else 'unknown'))
        stream = lib.parakeet_capi_stream_begin(ctx)
        if not stream:
            reason = lib.parakeet_capi_last_error(ctx)
            raise RuntimeError('Cache-aware stream start failed: ' + (reason.decode('utf-8', 'replace') if reason else 'unknown'))

        emit('READY')
        note(session, 'stream_ready')
        received = 0
        frames = 0
        utterance = 1
        utterance_start = 0
        pending_text = ''
        last_partial = ''

        def send_text(final, end_sample):
            nonlocal pending_text, last_partial, utterance, utterance_start
            value = pending_text.strip()
            if value and (final or value != last_partial):
                emit('FINAL' if final else 'PARTIAL', utterance, utterance_start, end_sample,
                     base64.b64encode(value.encode('utf-8')).decode('ascii'))
                last_partial = value
            if final:
                note(session, 'utterance_final', utterance=utterance, end_sample=end_sample,
                     characters=len(value))
                utterance += 1
                utterance_start = end_sample
                pending_text = ''
                last_partial = ''

        while True:
            size = struct.unpack('<I', exact(sys.stdin.buffer, 4))[0]
            if size == 0:
                break
            if size > 32000 or size % 2:
                raise ValueError('Invalid PCM16 packet size: ' + str(size))
            raw = exact(sys.stdin.buffer, size)
            samples = array.array('h')
            samples.frombytes(raw)
            floats = (C.c_float * len(samples))(*(sample / 32768.0 for sample in samples))
            event_mask = C.c_int()
            started = time.perf_counter()
            result = lib.parakeet_capi_stream_feed(stream, floats, len(samples), C.byref(event_mask))
            elapsed_ms = (time.perf_counter() - started) * 1000.0
            if not result:
                reason = lib.parakeet_capi_last_error(ctx)
                raise RuntimeError('Streaming decode failed: ' + (reason.decode('utf-8', 'replace') if reason else 'unknown'))
            try:
                text = C.string_at(result).decode('utf-8', 'replace')
            finally:
                lib.parakeet_capi_free_string(result)
            received += len(samples)
            frames += 1
            if text:
                pending_text += text
                send_text(False, received)
            # EOU (1) and EOB (2) both close this transcript segment. EOB is
            # retained as transcript text; it only denotes an ASR turn boundary.
            if event_mask.value & 3:
                send_text(True, received)
            if frames % 50 == 0:
                emit('PROGRESS', received)
                note(session, 'audio_progress', frames=frames, samples=received,
                     eou_mask=event_mask.value, feed_ms=round(elapsed_ms, 2))

        result = lib.parakeet_capi_stream_finalize(stream)
        if not result:
            reason = lib.parakeet_capi_last_error(ctx)
            raise RuntimeError('Stream finalization failed: ' + (reason.decode('utf-8', 'replace') if reason else 'unknown'))
        try:
            tail = C.string_at(result).decode('utf-8', 'replace')
        finally:
            lib.parakeet_capi_free_string(result)
        if tail:
            pending_text += tail
        send_text(True, received)
        emit('DONE')
        note(session, 'stream_done', frames=frames, samples=received)
    finally:
        if stream:
            lib.parakeet_capi_stream_free(stream)
        if ctx:
            lib.parakeet_capi_free(ctx)
        dll_directory.close()


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        emit('ERROR', base64.b64encode((type(exc).__name__ + ': ' + str(exc)).encode('utf-8')).decode('ascii'))
        print('Parakeet worker error: ' + traceback.format_exc(), file=sys.stderr, flush=True)
        raise
