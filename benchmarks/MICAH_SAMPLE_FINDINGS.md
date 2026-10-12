# Malachi / Micah recording: measured findings

2 October 2026. Recording supplied by the user: Sample 2 - PJoe Malachi-Micah,
96 seconds. SHA256 `9a0fe6b3c61a200e4c7bddb48a2f6c151c8d398ba67d04bec3cbddfbb1dc15f3`.
Audio and raw transcripts remain local under ignored `benchmarks/runs/`.

## Controlled comparison

Installed models only; no model downloads, microphone access or presentation commands.
Same recording decoded to PCM16 mono 16 kHz. Intact tests use whisper-cli;
live tests use production segmentation/queue code and whisper-server. Therefore
intact versus live also differs in inference invocation, not only window length.
No full verified transcript exists: these are targeted observations, not WER scores.

| Run | Observation | Timing |
|---|---|---|
| `micah-base-intact` | Recognized Malachi and Micah; damaged chapter/verse wording | 10.0 s to process 96 s audio; ~381 MB peak CLI working set |
| `micah-small-intact` | Recognized “Malachi chapter four verse one and two”; replaced correction with “right there” | 20.1 s batch processing; ~568 MB peak CLI working set |
| `micah-base-live` | Initial call became “my lagai, Dr. Forba's wanansu”; correction “no mica mica no mica”; zero explicit reference detections | p95 ready 1.83 s; p95 inference 1.09 s |
| `micah-small-live` | Initial call retained Malachi but corrupted chapter/verse; correction “No, mica, mica” | p95 ready 7.20 s; p95 inference 3.63 s |

Both live replays had zero dropped final windows and zero inference errors. This
rules out final-queue loss for these runs, not clipped syllables or segmentation
errors. Ready timing excludes desktop UI dispatch, quotation retrieval and adapter
actions. Batch throughput is not live latency. Models ran sequentially, not against
each other concurrently. Ordinary background system activity was not controlled.

The user confirmed a Malachi-to-Micah correction retaining chapter/verse. Models
disagree about parts of the call; their transcript is not ground truth. The later
mountain/house-of-the-Lord reading is consistent with Micah 4:1, with parallel wording
in Isaiah 2:2. Do not infer that the initial requested verse and subsequently read
verse must be identical.

## Implemented bounded fixes

- Explicit “no / sorry / correction / I mean / rather + book” can retain the
  recent valid chapter/verse, including an announced range. Same-utterance correction
  and repeated book names are supported. Corrected references require review.
- Normal book mentions do not inherit unrelated numbers. Invalid destination
  chapter/verse, expired context and ordinary trailing prose are rejected. Partial
  previews cannot mutate the correction anchor.
- Corrected book updates quotation scope. This is interpretation of recognized
  text; it does not repair the damaged audio transcription shown above.
- Strong near-tied lexical matches retain up to three review-only parallel passages
  instead of disappearing. Existing single-best gates and automation rules remain.
- Detection page 1 is newest-first after pinned entries. Latest returns to page 1
  and clears filtering; manual older-page browsing remains undisturbed. Automatic
  redraw still defers while an operator is interacting with a card.

## Next acoustic experiment

Keep base as the current default. Compare matched server settings on short versus
longer overlapping audio windows, then evaluate a bounded re-decode of retained
audio for uncertain reference calls. Measure correction recall and false references
as well as p95 latency. A larger model alone did not solve this recording; do not
switch production defaults based on model size or batch throughput.

Candidate changes are tested independently before activation. The application build
is first written as `Gazely.candidate.exe`; only a passing candidate may be staged
as `Gazely.pending.exe`. The normal launcher retains the prior executable when
activating the staged update. An interruption before staging leaves Gazely.exe intact.

Completion checkpoint, 6 October 2026: parser regression tests (including correction,
expiry and preview isolation), lexical/parallel-passage tests and the full interface
suite passed. `benchmarks/runs/ui-micah-replay` verifies that a recognized book
correction updates quotation scope and the actual replay's quoted fragments reach
Micah 4:1 without an automatic presentation command. This UI test supplies a clean
initial reference/correction; it does not claim the audio model recovered those words.
The candidate build succeeded and was staged as `Gazely.pending.exe`, with its hash
verified and the existing `Gazely.exe` confirmed unchanged. Close Gazely and launch
`RUN_GAZELY.bat` to activate. No model changes or downloads were made.
