# Gazely real-sermon benchmark — 30 September 2026

Follow-up: the user supplied an approximately 99% accurate corrected reference
after these original runs. It is preserved verbatim in
[reference-user-corrected.txt](reference-user-corrected.txt), with qualifications
in [reference-provenance.json](reference-provenance.json). Raw normalized WER is
59.24% for prompted base, 25.39% for unprompted base, 24.94% for unprompted base
with VAD, and 16.93% for small Q5_1. The `*.wer.json` files contain counts and
hashes. These are the original batch runs, not current live-pipeline results;
the original statements below about then-missing ground truth are historical.

## Outcome

The Bible vocabulary prompt reproduced invented names and runaway repetition on
the supplied recording. Removing it substantially improves the opening. The
quantized small English model improves several important phrases, but still
misrecognizes the Matthew quotation and other speech. No tested configuration
has demonstrated Pewbeam parity. Production source and executable are unchanged.

There are independent interpretation failures: the reported `verse CS3` is not
parseable, plural/range references are rejected, and quote retrieval is absent.
Simply installing a bigger model will not fix all three.

## Method and evidence

Original: `C:/Users/LIGHT NATION IKEJA/Documents/REAPER Media/test audio.mp3`.
Duration: 240 seconds. Original preserved; SHA-256 and source fingerprint in
[input.json](input.json). All inference was local; audio was not uploaded.

CPU: Intel i7-11800H, 8 cores / 16 logical processors; installed RAM approximately
16 GB. The machine also has RTX 3060 Laptop graphics, but these comparisons
explicitly disabled GPU inference. This is not validation on an ordinary 8 GB PC.

Runtime reports whisper.cpp 1.9.4, CPU cascadelake backend. Compare actual runtime
identity with the older b5130 setup pin before changing deployment dependencies.
CLI: English, eight threads, beam three, best-of one, no temperature fallback,
non-speech token suppression. The three controlled base comparisons use identical
decoded 16 kHz mono PCM. Initial `base-no-prompt.*` was an exploratory MP3-direct
run; it is excluded from the table because decoding and timing differ.

| Variant | File size | Full 240s replay time | Observation |
|---|---:|---:|---|
| Base + existing Bible prompt | 148 MB | 19.84 s | Invented opening names; extensive repeated “chapter”; later repeated sentences |
| Base, no prompt | 148 MB | 18.01 s | Coherent opening and reference; many remaining word errors |
| Base, no prompt + Silero VAD | 148 MB | 17.12 s | Extracts Genesis 49 verse 3 and verse 4; substantial recognition errors remain |
| Small.en Q5_1, no prompt | 190 MB | 59.53 s | Better Reuben, might, excellency, unstable as water; still misses important words |

These are single-run batch throughput measurements, not live display latency.
They include process/model startup. Context and decoding differ from the live
server's short windows. Do not attribute the entire live-versus-batch difference
to the prompt alone. The same-PCM prompt comparison is the controlled evidence
for prompt harm on this sample.

Small-model peak process working set observed by Windows was 583,647,232 bytes
(about 557 MiB). This excludes Gazely, presentation software, OS memory and other
processes. Earlier base runs did not collect peak working set. Download size is
not total installation size. Model hashes and exact arguments are in each
`*.metrics.json`; transcripts and CLI logs are retained alongside them.

Silero removed 21.9% of the input samples before inference. That is its classifier
decision, not proof that all removed audio was noise; check missed quiet speech
before adopting its default thresholds. This experiment does not establish a
false-positive rate on the live microphone's background-noise tail.

The supplied Pewbeam transcript is a comparator, not a manually corrected
reference transcript. No numerical WER or “98% achieved” claim is justified.
The original file and live microphone feed can also differ through capture and
segmentation; this replay does not invalidate the user's same-output comparison.

## Concrete recognition differences

The prompted base output starts with “Jesus, Nebuchadnezzar, Benedict, Psalms”
and repeats “chapter” dozens of times. Unprompted base starts “All right, I
continue on the subject of meditation”. Both used the same decoded waveform.

Small correctly recovers “Reuben”, “my might”, “excellency”, “unstable as water”,
and “the main thing” where base makes important errors. It still produces
“safety affects the kingdom” and “I'm not sensitive”. It is a promising candidate,
not a finished answer or a model ready to declare the new default.

## Actual live-session evidence

[Controller excerpt](controller-session-excerpt.log) preserves the relevant
existing log. It confirms base.en and CPU inference for this session.

- At 12:01:56: `In Genesis, chapter 49 verse CS3 and verse 4.`; parser reports
  no complete Bible reference. Inference 1,141 ms; logged ready time 1,540 ms.
- At 12:05:24: repeated `Nephi`; 2.84 seconds of audio takes 3,654 ms inference
  and 4,678 ms logged ready time. The user identifies this as background noise.
- Across 39 recorded final timing entries in the excerpt, mean ready time is
  1,789 ms, maximum 4,678 ms. These values are the app's own timing fields, not
  latency from the first spoken word, and include one reused result with zero
  recorded inference. Raw values: [controller-timing.csv](controller-timing.csv).

## Scripture interpretation

[Parser probes](parser-probes.csv) were executed against the current source:

| Input | Current result |
|---|---|
| Genesis chapter 49 verse CS3 and verse 4 | No match |
| Genesis chapter 49 verses three and verse four | No match |
| Genesis 49:3-4 | No match |
| Genesis chapter 49 verse three and verse four | Genesis 49:3 only |
| Genesis chapter 49 verse three | Genesis 49:3 |
| Seek ye first the kingdom… | No match |
| Unstable as water thou shalt not excel | No match |

The singular wording can silently lose the second verse, while plural wording
is rejected. Range handling must be modeled explicitly, not “fixed” by blindly
replacing plurals or deleting unrecognized characters such as `CS`.

Based on the supplied sermon text, the evaluation should include Genesis 49:3–4,
the quoted passage in that chapter, and the Matthew 6:33 quotation. Annotate their
audio locations and exact wording before scoring retrieval. The many Pewbeam
cards pasted by the user are candidate suggestions, not verified positive labels.
For example, Ephesians 5:30 and 1 Samuel 19:3 have no supporting quotation in the
provided excerpt despite high displayed percentages. Count unrelated suggestions
as false positives when evaluating the same time interval.

## Implementation sequence

1. Replace the global Bible-list prompt with an unprompted baseline. Put optional
   terminology hints behind measured evaluations, separate from literal text.
2. Integrate neural speech detection with pre-roll and sufficient padding;
   test quiet speech and pauses. Reject pathological repetition/low-evidence
   output before transcript/context/detection, without deleting legitimate repeats.
3. Replay real audio through the live segmenter and persistent server, logging
   queued time, inference, committed-text time and reference time. Compare context
   preservation and candidate model latency. Batch speed is insufficient evidence.
4. Add a structured verse-range result and cross-segment reference assembly;
   preserve both verses and operator control in the EasyWorship adapter.
5. Add local quote retrieval over a licensed Bible corpus: candidate generation,
   evidence-based reranking, context and abstention. Keep semantic suggestions
   distinct from explicit references; no automatic live action from a loose match.
6. Establish corrected speech/reference labels, held-out speakers and noise-only
   tests; measure WER, reference precision/recall, false detections per hour,
   latency percentiles and memory alongside EasyWorship on an 8 GB CPU-only PC.

Keep small Q5_1 as a candidate. Evaluate a stronger quantized model only against
these same quality, download, memory and live-latency requirements. Preserve the
tested presentation adapter while improving the recognition/understanding layers.

## Reproduction

Evaluation tooling only: Python 3.12 and the pinned packages in
`../requirements.txt`, installed into `../tools/python`. Users of Gazely do not
need Python. `../benchmark_sermon.py AUDIO` runs prompted base and VAD base;
`--baseline` runs unprompted PCM base; `--small` runs the compressed small model.
`../Probe-SermonParser.ps1` regenerates the interpretation probes.

Runtime and base/VAD models use the existing application folders. Downloaded
small candidate is in ignored `../tools/ggml-small.en-q5_1.bin`, from the
[whisper.cpp model repository](https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-small.en-q5_1.bin).
Recorded SHA-256 identifies the local model; no independent remote checksum
comparison was performed. Generated decoded audio and local dependencies are
ignored; transcripts, logs, metrics and this report are retained.
