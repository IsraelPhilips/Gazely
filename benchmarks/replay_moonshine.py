"""Offline native-streaming evaluation; no microphone, network or presentation.

Use a separately downloaded model. Accelerated runs measure throughput, not live
latency. Final events are immutable; stop() flushes the ending audio. RAM is for
this worker only. Reference-end latency needs separately verified audio labels.
"""
import argparse, csv, hashlib, importlib.metadata, json, pathlib, threading, time, wave
import numpy as np
import psutil
import base64, subprocess, queue, os
from moonshine_voice import Transcriber, ModelArch, TranscriptEventListener

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def run(a):
    components = {p.name: {'bytes': p.stat().st_size, 'sha256': sha(p)}
                  for p in a.model.iterdir() if p.is_file()}
    lock_path=a.model_lock or pathlib.Path(__file__).with_name('moonshine-medium-manifest.json')
    locked=json.loads(lock_path.read_text(encoding='utf-8-sig'))
    for name, expected in locked['components'].items():
        if components.get(name)!=expected: raise ValueError('Model integrity mismatch: '+name)
    version=importlib.metadata.version('moonshine-voice')
    if locked['runtime']!='moonshine-voice=='+version: raise ValueError('Runtime differs from tested lock')
    a.output.mkdir(parents=True, exist_ok=False)
    manifest = dict(engine='moonshine-voice', version=version,
                    architecture=locked['architecture'],model_lock_sha256=sha(lock_path),
                    audio=str(a.audio.resolve()), audio_sha256=sha(a.audio),
                    model=str(a.model.resolve()), components=components, mode=a.mode,
                    keyterms=a.keyterms, vad_threshold=a.vad_threshold, update_interval=a.update_interval, frame_ms=100,
                    eof_silence_ms=1000, presentation=False, retrieval=bool(a.retrieval),
                    harness_sha256=sha(pathlib.Path(__file__)),
                    ort_single_thread=os.environ.get('MOONSHINE_ORT_SINGLE_THREAD',''),
                    note='Worker-only test. Emission time is not verified spoken-reference latency.')
    (a.output/'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    process=psutil.Process(); peak=[0]; combined_peak=[0]; done=threading.Event()
    def sample():
        while not done.wait(.05):
            peak[0]=max(peak[0], process.memory_info().rss)
            total=process.memory_info().rss
            for child in process.children(recursive=True):
                try: total+=child.memory_info().rss
                except psutil.NoSuchProcess: pass
            combined_peak[0]=max(combined_peak[0],total)
    monitor=threading.Thread(target=sample, daemon=True); monitor.start()
    events=[]; finals={}; errors=[]; cursor=[0.0]; started=[None]; max_lag=0.0; feed_rows=[]
    search=None; search_thread=None; search_queue=queue.Queue(maxsize=1)
    search_rows=[]; search_superseded=0
    if a.retrieval:
        root=a.retrieval.resolve()
        search_log=(a.output/'retrieval-stderr.txt').open('w',encoding='utf-8')
        search=subprocess.Popen([str(root/'search-runtime/Gazely.Search.exe'),
            str(root/'models/scripture-search'),str(root/'corpus/kjv.tsv')],
            stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=search_log,
            text=True,encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW)
        def retrieve():
            try:
                if search.stdout.readline().strip()!='READY': raise RuntimeError('Search initialization failed')
                while True:
                    item=search_queue.get()
                    if item is None: break
                    text_value,queued=item; t=time.perf_counter()
                    search.stdin.write(base64.b64encode(text_value.encode()).decode()+'\n');search.stdin.flush()
                    response=search.stdout.readline()
                    result=response.strip()
                    if response=='' or result=='ERROR': raise RuntimeError('Search response failed')
                    search_rows.append(dict(text=text_value,response=result,queue_ms=(t-queued)*1000,
                        search_ms=(time.perf_counter()-t)*1000))
            except Exception as exc: errors.append('Retrieval: '+str(exc))
        search_thread=threading.Thread(target=retrieve,daemon=True);search_thread.start()
    class Listener(TranscriptEventListener):
        def record(self, kind, event):
            nonlocal search_superseded
            line=event.line
            row=dict(event=kind,line_id=str(line.line_id),audio_start_ms=line.start_time*1000,
                     audio_end_ms=(line.start_time+line.duration)*1000,
                     input_ms=cursor[0]*1000,wall_ms=(time.perf_counter()-started[0])*1000,
                     text=line.text)
            events.append(row)
            if kind=='final':
                if row['line_id'] in finals:
                    errors.append('Duplicate final line '+row['line_id'])
                    return
                finals[row['line_id']]=row
                print(line.text, flush=True)
                if search:
                    try: search_queue.put_nowait((line.text,time.perf_counter()))
                    except queue.Full:
                        try: search_queue.get_nowait();search_superseded+=1
                        except queue.Empty: pass
                        search_queue.put_nowait((line.text,time.perf_counter()))
        def on_line_text_changed(self,event): self.record('partial',event)
        def on_line_completed(self,event): self.record('final',event)
        def on_error(self,event): errors.append(str(event))
    options={'keyterms': a.keyterms} if a.keyterms else {}
    if a.vad_threshold is not None: options['vad_threshold']=a.vad_threshold
    load_start=time.perf_counter()
    transcriber=Transcriber(model_path=a.model,model_arch=ModelArch[locked['architecture']],
                            options=options,update_interval=a.update_interval)
    load_seconds=time.perf_counter()-load_start
    transcriber.add_listener(Listener())
    # Library swallows callback exceptions; capture them through a second listener.
    transcriber.add_listener(lambda event: errors.append(str(event)) if type(event).__name__=='Error' else None)
    cpu_start=process.cpu_times(); started[0]=time.perf_counter()
    duration=0.0; failure=None
    try:
        with wave.open(str(a.audio)) as f:
            if (f.getnchannels(),f.getsampwidth(),f.getframerate())!=(1,2,16000):
                raise ValueError('Expected PCM16 mono 16kHz input')
            duration=f.getnframes()/16000
            transcriber.start()
            while True:
                raw=f.readframes(1600)
                if not raw: break
                data=np.frombuffer(raw,dtype='<i2').astype(np.float32)/32768
                cursor[0]+=len(data)/16000
                if a.mode=='realtime':
                    time.sleep(max(0,started[0]+cursor[0]-time.perf_counter()))
                feed_started=time.perf_counter()
                transcriber.add_audio(data,16000)
                feed_finished=time.perf_counter()
                lag=max(0,feed_finished-started[0]-cursor[0])
                max_lag=max(max_lag,lag)
                feed_rows.append(dict(input_seconds=cursor[0],call_seconds=feed_finished-feed_started,
                                      lag_seconds=lag if a.mode=='realtime' else None))
            for _ in range(10):
                cursor[0]+=.1
                if a.mode=='realtime': time.sleep(max(0,started[0]+cursor[0]-time.perf_counter()))
                transcriber.add_audio(np.zeros(1600,dtype=np.float32),16000)
            transcriber.stop()
    except Exception as exc:
        failure=exc
        errors.append(type(exc).__name__+': '+str(exc))
    finally:
        try:
            transcriber.close()
        finally:
            if search:
                try:
                    search_queue.put(None,timeout=30)
                    search_thread.join(30)
                    if search_thread.is_alive(): errors.append('Retrieval drain timed out')
                except queue.Full: errors.append('Retrieval queue failed to drain')
                finally:
                    if search.poll() is None: search.kill()
                    search.wait();search_thread.join(5);search_log.close()
            done.set(); monitor.join()
    elapsed=time.perf_counter()-started[0]; cpu_end=process.cpu_times()
    peak[0]=max(peak[0],getattr(process.memory_info(),'peak_wset',0))
    with (a.output/'events.tsv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=['event','line_id','audio_start_ms','audio_end_ms','input_ms','wall_ms','text'],delimiter='\t')
        writer.writeheader(); writer.writerows(events)
    with (a.output/'transcript.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=['audio_start_ms','audio_end_ms','text'])
        writer.writeheader();writer.writerows({k:r[k] for k in writer.fieldnames} for r in finals.values())
    with (a.output/'feed.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=['input_seconds','call_seconds','lag_seconds'])
        writer.writeheader();writer.writerows(feed_rows)
    summary=dict(audio_seconds=duration,elapsed_seconds=elapsed,load_seconds=load_seconds,
                 realtime_factor=elapsed/duration if duration else None,peak_worker_ram_bytes=peak[0],
                 cpu_seconds=cpu_end.user+cpu_end.system-cpu_start.user-cpu_start.system,
                 maximum_feed_lag_seconds=max_lag if a.mode=='realtime' else None,
                 finals=len(finals),errors=errors,completed=failure is None and not errors,
                 input_seconds=cursor[0],
                 sampled_combined_ram_bytes=combined_peak[0],retrieval_queries=len(search_rows),
                 retrieval_superseded=search_superseded,
                 timing_note='Feed lag includes synchronous inference; not reference-display latency. Optional semantic worker contention excludes UI and lexical retrieval.')
    (a.output/'retrieval.json').write_text(json.dumps(search_rows,indent=2),encoding='utf-8')
    (a.output/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(summary,indent=2))
    if failure: raise RuntimeError('Replay failed; partial evidence saved') from failure
    if errors: raise RuntimeError('Moonshine reported errors')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('audio',type=pathlib.Path);p.add_argument('model',type=pathlib.Path)
    p.add_argument('output',type=pathlib.Path);p.add_argument('--mode',choices=['accelerated','realtime'],default='accelerated')
    p.add_argument('--keyterms',default='');p.add_argument('--vad-threshold',type=float)
    p.add_argument('--retrieval',type=pathlib.Path,help='App directory containing installed offline search runtime')
    p.add_argument('--update-interval',type=float,choices=[.5,.75,1.0],default=.5)
    p.add_argument('--model-lock',type=pathlib.Path,help='Verified model component manifest; defaults to Medium')
    run(p.parse_args())
