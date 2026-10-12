"""Local transport regression: paced silence, EOF acknowledgement, abrupt cleanup.
Run with benchmarks/tools/parakeet-env/Scripts/python.exe. No microphone/output.
"""
import base64
import pathlib
import queue
import struct
import subprocess
import sys
import threading
import time
import unittest

import psutil

ROOT = pathlib.Path(__file__).resolve().parent.parent
WORKER = ROOT / 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/speech-runtime/nemotron_worker.py'
ASSETS = ROOT / 'benchmarks/tools/streaming-candidates'
MANIFEST = ROOT / 'benchmarks/streaming-candidates-manifest.json'


class WorkerTests(unittest.TestCase):
    def start_worker(self):
        proc = subprocess.Popen([sys.executable, '-u', str(WORKER), str(ASSETS), str(MANIFEST)],
                                stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                stderr=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        self.addCleanup(self.stop_worker, proc)
        messages = queue.Queue()

        def read():
            for line in proc.stdout:
                messages.put(line.decode().rstrip('\r\n').split('\t'))
            messages.put(['EXIT'])

        threading.Thread(target=read, daemon=True).start()
        native = None
        while True:
            row = messages.get(timeout=60)
            if row[0] == 'SERVER':
                native = psutil.Process(int(row[1]))
            elif row[0] == 'READY':
                self.assertIsNotNone(native)
                return proc, messages, native
            elif row[0] in ('ERROR', 'EXIT'):
                self.fail(str(row))

    @staticmethod
    def stop_worker(proc):
        if proc.poll() is None:
            proc.kill()
        proc.wait(timeout=10)
        proc.stdin.close()
        proc.stdout.close()

    def test_silence_has_progress_and_flushes_without_inventing_words(self):
        proc, messages, native = self.start_worker()
        start = time.monotonic()
        for index in range(100):
            proc.stdin.write(struct.pack('<I', 3200) + bytes(3200))
            proc.stdin.flush()
            time.sleep(max(0, start + (index + 1) / 10 - time.monotonic()))
        proc.stdin.write(struct.pack('<I', 0))
        proc.stdin.flush()
        acknowledged = 0
        while True:
            row = messages.get(timeout=30)
            if row[0] == 'PROGRESS':
                acknowledged = max(acknowledged, int(row[1]))
            elif row[0] in ('PARTIAL', 'FINAL'):
                self.assertEqual(base64.b64decode(row[4]).decode().strip(), '')
            elif row[0] == 'DONE':
                break
            elif row[0] in ('ERROR', 'EXIT'):
                self.fail(str(row))
        self.assertEqual(acknowledged, 160000)
        self.assertEqual(proc.wait(timeout=10), 0)
        native.wait(timeout=10)

    def test_worker_kill_removes_native_child(self):
        proc, _, native = self.start_worker()
        proc.kill()
        proc.wait(timeout=10)
        native.wait(timeout=10)
        self.assertFalse(native.is_running())


if __name__ == '__main__':
    unittest.main(verbosity=2)
