"""Local-only recording comparison. Python is evaluation tooling, not a product dependency."""
import argparse, ctypes, hashlib, json, pathlib, re, subprocess, sys, time, wave
from ctypes import wintypes

class MemoryCounters(ctypes.Structure):
    _fields_ = [('cb',wintypes.DWORD),('PageFaultCount',wintypes.DWORD)] + [(name,ctypes.c_size_t) for name in ('PeakWorkingSetSize','WorkingSetSize','QuotaPeakPagedPoolUsage','QuotaPagedPoolUsage','QuotaPeakNonPagedPoolUsage','QuotaNonPagedPoolUsage','PagefileUsage','PeakPagefileUsage')]

memory_info = ctypes.windll.psapi.GetProcessMemoryInfo
memory_info.argtypes = [wintypes.HANDLE,ctypes.POINTER(MemoryCounters),wintypes.DWORD]

ROOT = pathlib.Path(__file__).resolve().parent.parent
APP = ROOT / 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
sys.path.insert(0, str(ROOT / 'benchmarks/tools/python'))
import miniaudio

def digest(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024*1024), b''): h.update(block)
    return h.hexdigest()

p = argparse.ArgumentParser()
p.add_argument('audio', type=pathlib.Path)
p.add_argument('--small', action='store_true')
p.add_argument('--baseline', action='store_true')
p.add_argument('--output-dir',type=pathlib.Path,help='New evidence directory; existing files are never overwritten')
a = p.parse_args()
out = a.output_dir or ROOT / 'benchmarks/runs' / ('batch-'+time.strftime('%Y%m%d-%H%M%S'))
if out.exists() and any(out.iterdir()): raise SystemExit('Choose an empty/new output directory to preserve evidence.')
out.mkdir(parents=True, exist_ok=True)
decoded = miniaudio.decode_file(str(a.audio), output_format=miniaudio.SampleFormat.SIGNED16, nchannels=1, sample_rate=16000)
pcm = decoded.samples.tobytes()
duration = len(pcm)/32000
source = (APP/'EasyWorshipController.cs').read_text(encoding='utf-8-sig')
prompt_block = source.split('public const string Prompt =',1)[1].split(';',1)[0]
prompt = ''.join(re.findall(r'"([^"]*)"',prompt_block))
metadata = {'audio':str(a.audio),'audio_sha256':digest(a.audio),'duration_seconds':duration,
    'sample_rate':16000,'channels':1,'prompt':prompt,'source_sha256':digest(APP/'EasyWorshipController.cs'),
    'note':'CPU-only CLI replay, not end-to-end microphone latency. One run per variant; no WER ground truth.'}
(out/'input.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
wav = out/'decoded.wav'
with wave.open(str(wav),'wb') as f:
    f.setnchannels(1); f.setsampwidth(2); f.setframerate(16000); f.writeframes(pcm)
cli = APP/'whisper/runtime/Release/whisper-cli.exe'
base = APP/'models/ggml-base.en.bin'
vad = APP/'models/ggml-silero-v6.2.0.bin'
variants = [('base-production-prompt',base,['--prompt',prompt]),('base-vad-no-prompt',base,['--vad','-vm',str(vad)])]
if a.small:
    variants = [('small-q5-no-prompt',APP/'models/ggml-small.en-q5_1.bin',[])]
if a.baseline:
    variants = [('base-pcm-no-prompt',base,[])]
for name,model,extra in variants:
    args = [str(cli),'-m',str(model),'-f',str(wav),'-t','8','-bs','3','-bo','1','-nf','-ng','-l','en','-sns','-otxt','-oj','-of',str(out/name)] + extra
    start = time.perf_counter()
    peak = 0
    with open(out/(name+'.log'),'w',encoding='utf-8') as log:
        process = subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT)
        while process.poll() is None:
            counters = MemoryCounters()
            counters.cb = ctypes.sizeof(counters)
            if memory_info(int(process._handle),ctypes.byref(counters),counters.cb):
                peak = max(peak,counters.PeakWorkingSetSize)
            time.sleep(.1)
    elapsed = time.perf_counter()-start
    result = {'name':name,'exit_code':process.returncode,'wall_seconds':elapsed,'real_time_factor':elapsed/duration,'peak_working_set_bytes':peak or None,'model_bytes':model.stat().st_size,'model_sha256':digest(model),'args':args}
    (out/(name+'.metrics.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='args'}),flush=True)
    if process.returncode: raise SystemExit(process.returncode)
