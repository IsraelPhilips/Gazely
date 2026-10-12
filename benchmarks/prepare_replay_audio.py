"""Decode a recording locally to the production capture format; preserve original."""
import argparse, pathlib, sys, wave
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent/'tools/python'))
import miniaudio
p=argparse.ArgumentParser()
p.add_argument('source',type=pathlib.Path)
p.add_argument('destination',type=pathlib.Path)
p.add_argument('--start',type=float,default=0,help='Start offset in seconds')
p.add_argument('--duration',type=float,help='Optional excerpt duration in seconds')
a=p.parse_args()
if a.start < 0 or (a.duration is not None and a.duration <= 0):
    raise SystemExit('Start must be nonnegative and duration must be positive.')
if a.destination.exists(): raise SystemExit('Destination already exists; choose a new path.')
decoded=miniaudio.decode_file(str(a.source),output_format=miniaudio.SampleFormat.SIGNED16,nchannels=1,sample_rate=16000)
start=int(a.start*16000)
end=len(decoded.samples) if a.duration is None else start+int(a.duration*16000)
samples=decoded.samples[start:end]
if not samples: raise SystemExit('Requested excerpt is outside the recording.')
a.destination.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(a.destination),'wb') as f:
    f.setnchannels(1);f.setsampwidth(2);f.setframerate(16000);f.writeframes(samples.tobytes())
print(f'{len(samples)/16000:.3f} seconds from offset {a.start:.3f}, PCM16 mono 16000 Hz')
