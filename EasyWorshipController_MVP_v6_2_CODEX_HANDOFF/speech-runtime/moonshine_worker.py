"""Offline PCM streaming bridge. stdout is a protocol, diagnostics go to stderr.
One native stream per process/session. A zero-length packet flushes and exits.
No microphone capture, audio files, downloads or presentation commands.
"""
import base64, hashlib, importlib.metadata, json, pathlib, struct, sys
import numpy as np
from moonshine_voice import Transcriber, ModelArch, TranscriptEventListener

def emit(*parts):
    print('\t'.join(str(p) for p in parts),flush=True)

def exact(stream,count):
    data=bytearray()
    while len(data)<count:
        block=stream.read(count-len(data))
        if not block: raise EOFError('Truncated PCM packet')
        data.extend(block)
    return data

def main():
    model=pathlib.Path(sys.argv[1]); lock=json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
    if lock['runtime']!='moonshine-voice=='+importlib.metadata.version('moonshine-voice'):
        raise ValueError('Moonshine runtime does not match the tested version')
    for name,expected in lock['components'].items():
        path=model/name
        with path.open('rb') as source: digest=hashlib.file_digest(source,'sha256').hexdigest()
        if path.stat().st_size!=expected['bytes'] or digest!=expected['sha256']:
            raise ValueError('Model integrity mismatch: '+name)
    identities={}; completed=set(); errors=[]
    class Listener(TranscriptEventListener):
        def send(self,event,final):
            line=event.line; key=line.line_id
            if key in completed: return
            if key not in identities: identities[key]=len(identities)+1
            if final: completed.add(key)
            emit('FINAL' if final else 'PARTIAL',identities[key],round(line.start_time*16000),
                 round((line.start_time+line.duration)*16000),
                 base64.b64encode(line.text.encode('utf-8')).decode('ascii'))
        def on_line_text_changed(self,event): self.send(event,False)
        def on_line_completed(self,event): self.send(event,True)
        def on_error(self,event): errors.append(str(event))
    transcriber=Transcriber(model_path=model,model_arch=ModelArch[lock['architecture']],update_interval=.5)
    transcriber.add_listener(Listener())
    transcriber.add_listener(lambda event: errors.append(str(event)) if type(event).__name__=='Error' else None)
    try:
        transcriber.start();emit('READY')
        while True:
            size=struct.unpack('<I',exact(sys.stdin.buffer,4))[0]
            if size==0: break
            if size>32000 or size%2: raise ValueError('Invalid PCM16 packet')
            pcm=exact(sys.stdin.buffer,size)
            transcriber.add_audio(np.frombuffer(pcm,dtype='<i2').astype(np.float32)/32768,16000)
            if errors: raise RuntimeError('; '.join(errors))
        for _ in range(10): transcriber.add_audio(np.zeros(1600,dtype=np.float32),16000)
        transcriber.stop()
        if errors: raise RuntimeError('; '.join(errors))
        emit('DONE')
    finally: transcriber.close()

if __name__=='__main__':
    try: main()
    except Exception as exc:
        emit('ERROR',base64.b64encode((type(exc).__name__+': '+str(exc)).encode()).decode())
        raise
