"""Pinned native CPU streaming replay. Throughput, not microphone latency.

No microphone, model downloads, presentation actions or production configuration.
Run configurations sequentially. Retrieval contention is a separate pending gate.
"""
import argparse, hashlib, json, pathlib, subprocess, time, wave
import psutil

def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def run(a):
    root=pathlib.Path(__file__).resolve().parent
    assets=root/'tools/streaming-candidates'
    model=assets/'nemotron/nemotron-speech-streaming-en-0.6b.q8_0.gguf'
    lock=json.loads((root/'streaming-candidates-manifest.json').read_text(encoding='utf-8-sig'))
    expected=next(x for x in lock if x['path'].endswith('.gguf'))
    if digest(model)!=expected['sha256']: raise ValueError('Model checksum mismatch')
    exe=next((assets/'nemo-runtime').rglob('nemo-speech.exe'))
    a.output.mkdir(parents=True,exist_ok=False)
    with wave.open(str(a.audio)) as f: seconds=f.getnframes()/f.getframerate()
    command=[str(exe),'transcribe',str(a.audio.resolve()),'--model',str(model),
             '--device','cpu','--stream','--asr.backend.threads',str(a.threads),
             '--asr.streaming.rnnt_right_context',str(a.context),'--format','json',
             '--output',str((a.output/'result.json').resolve())]
    manifest=dict(audio=str(a.audio),audio_sha256=digest(a.audio),audio_seconds=seconds,
                  model_sha256=expected['sha256'],runtime_sha256=digest(exe),command=command,
                  mode='accelerated native streaming',retrieval=False,presentation=False,
                  note='Includes startup; not live reference latency or a 30-minute stability test.')
    (a.output/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    peak=cpu=0;started=time.perf_counter()
    with (a.output/'runtime.log').open('w',encoding='utf-8') as log:
        proc=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
        watched=psutil.Process(proc.pid)
        try:
            while proc.poll() is None:
                try:
                    peak=max(peak,watched.memory_info().rss)
                    t=watched.cpu_times();cpu=t.user+t.system
                except psutil.NoSuchProcess: pass
                if time.perf_counter()-started>1800: raise TimeoutError('Replay exceeded 30 minutes')
                time.sleep(.05)
        finally:
            if proc.poll() is None: proc.kill();proc.wait()
    elapsed=time.perf_counter()-started
    summary=dict(exit_code=proc.returncode,elapsed_seconds=elapsed,audio_seconds=seconds,
                 audio_per_wall_second=seconds/elapsed,peak_worker_rss_bytes=peak,sampled_cpu_seconds=cpu,
                 threads=a.threads,right_context=a.context)
    (a.output/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    if proc.returncode: raise RuntimeError('Native replay failed; inspect runtime.log')
    result=json.loads((a.output/'result.json').read_text(encoding='utf-8'))
    (a.output/'transcript.txt').write_text(result['text'],encoding='utf-8')
    print(json.dumps(summary,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('audio',type=pathlib.Path);p.add_argument('output',type=pathlib.Path)
    p.add_argument('--threads',type=int,default=4);p.add_argument('--context',type=int,default=1)
    run(p.parse_args())
