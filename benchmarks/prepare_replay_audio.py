"""Decode a recording locally to the production capture format; preserve original."""
import argparse, pathlib, sys, wave
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent/'tools/python'))
import miniaudio
p=argparse.ArgumentParser()
p.add_argument('source',type=pathlib.Path)
p.add_argument('destination',type=pathlib.Path)
a=p.parse_args()
if a.destination.exists(): raise SystemExit('Destination already exists; choose a new path.')
decoded=miniaudio.decode_file(str(a.source),output_format=miniaudio.SampleFormat.SIGNED16,nchannels=1,sample_rate=16000)
a.destination.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(a.destination),'wb') as f:
    f.setnchannels(1);f.setsampwidth(2);f.setframerate(16000);f.writeframes(decoded.samples.tobytes())
print(f'{len(decoded.samples)/16000:.3f} seconds, PCM16 mono 16000 Hz')
