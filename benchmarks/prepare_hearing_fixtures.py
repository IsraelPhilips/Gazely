"""Make local PCM fixtures from previously decoded user recordings, without overwriting.
Development excerpts tune settings; the remaining excerpts are reserved for this phase.
This manifest is provenance, not a fabricated accurate transcript.
"""
import argparse, hashlib, json, math, pathlib, random, struct, wave

ROOT=pathlib.Path(__file__).resolve().parent
def read(path,start,duration):
    with wave.open(str(path)) as f:
        if (f.getnchannels(),f.getsampwidth(),f.getframerate())!=(1,2,16000):raise ValueError('Expected PCM16 mono 16kHz')
        f.setpos(round(start*16000));return f.readframes(round(duration*16000))
def write(path,data):
    if path.exists():raise FileExistsError(path)
    with wave.open(str(path),'wb') as f:
        f.setparams((1,2,16000,0,'NONE','not compressed'));f.writeframes(data)

def prepare(destination):
    destination.mkdir(parents=True,exist_ok=False)
    sources={'genesis':ROOT/'2026-09-30-sermon/decoded.wav','micah':ROOT/'runs/micah-base-intact/decoded.wav','leviticus':ROOT/'runs/leviticus-source/decoded.wav'}
    splits={'development':[('genesis',27,12),('micah',10,27),('leviticus',128,48)],
            'reserved':[('genesis',60,180),('micah',40,56),('leviticus',0,120)]}
    manifest={}
    for split,clips in splits.items():
        data=b'';entries=[]
        for name,start,duration in clips:
            pcm=read(sources[name],start,duration)
            entries.append(dict(source=name,source_start_seconds=start,output_start_seconds=len(data)/32000,duration_seconds=len(pcm)/32000,
                                source_sha256=hashlib.sha256(sources[name].read_bytes()).hexdigest()))
            data+=pcm+bytes(5*32000)
        write(destination/(split+'.wav'),data);manifest[split]=entries
    rng=random.Random(491)
    for name in ('silence','noise','hum'):
        data=bytearray()
        for i in range(10*16000):
            sample=0 if name=='silence' else rng.randint(-1600,1600) if name=='noise' else round(1600*math.sin(2*math.pi*100*i/16000))
            data.extend(struct.pack('<h',sample))
        write(destination/(name+'.wav'),data)
    (destination/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('destination',type=pathlib.Path);prepare(p.parse_args().destination)
