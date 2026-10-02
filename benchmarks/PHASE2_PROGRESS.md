# Phase 2 — first measured speech improvements

Status: in progress. First evaluation build delivered; quality targets not met.

## Reference and method

The user's [corrected transcript](2026-09-30-sermon/reference-user-corrected.txt)
contains 449 normalized word tokens and is assessed by the user as approximately
99% accurate. Preserve its uncertain ending, disfluencies and spelling; no silent
corrections. Scoring ignores punctuation/case but retains numbers and contractions.
Whole-recording alignment is assumed, not independently verified. WER counts
substitutions, deletions and insertions; it is not an informal accuracy percentage.

Each full live replay used the same 240-second decoded recording, real-time
40 ms framing, base.en, current energy endpointing, queue policy and parser.
No presentation adapter is included in the replay executable. These are single
runs on the current 16 GB laptop, not proof of ordinary 8 GB performance.

| Variant | Word error rate | Median ready | p95 ready | Final results / detections |
|---|---:|---:|---:|---:|
| [Original pipeline](runs/phase1-sermon-realtime/summary.json) | 45.88% | 2.20 s | 3.75 s | 52 / 0 |
| [No Bible prompt](runs/phase2-no-prompt/summary.json) | 40.31% | 1.25 s | 1.67 s | 52 / 0 |
| [No prompt + neural VAD + repetition guard](runs/phase2-neural-vad/summary.json) | 45.88% | 1.30 s | 1.66 s | 49 / 0 |
| [Small.en Q5_1, no prompt, current small profile](runs/phase2-small-live/summary.json) | 30.73% | 3.38 s | 5.44 s | 51 / 0 |
| [Base, 800 ms endpoint / 12 s cap](runs/phase2-base-long-context/summary.json) | 35.19% | 1.58 s | 1.98 s | 31 / 0 |

Ready starts at the last energy-classified voice frame and excludes UI dispatch
and EasyWorship action time. It is not latency from a manually labeled reference
endpoint. No final windows were dropped and no inference errors occurred in any
of these runs. Initial/replayed process load differs slightly; repeat performance
tests on target hardware before release claims.

Prompt removal reduces WER by 5.57 percentage points on this sample and substantially
reduces delay. It is insufficient to reach the accuracy target. Neural VAD with
threshold .5, minimum speech 160 ms, silence 400 ms, padding 200 ms and overlap
.1 s rejected three final windows but increased overall word errors. Do not infer
that every rejected window was noise, or that successful synthetic noise rejection
establishes accuracy in church audio.

Per-run `wer-user-reference.json`, `events.tsv`, `transcript.csv`, `resources.tsv`
and `server.log` retain counts, hashes and diagnostics. Peak server working set
was approximately 294 MiB without the prompt and 300 MiB with VAD, excluding UI,
EasyWorship and OS. Full-file batch small Q5 reached 16.93% WER against this same
reference, but that result benefits from different context and is not live parity.

## Changes kept in the evaluation build

Follow-up model comparison: the existing 190,098,681-byte small Q5_1 candidate
was checksum-checked and moved into the application models directory, with no
additional download or duplicate copy. The **More accurate** profile now accepts
`ggml-small.en-q5_1.bin` when the full `ggml-small.en.bin` is absent. Existing
full-precision small installations retain priority. Balanced remains the default.

The small-model live replay reached 30.73% WER (85 substitutions, 11 deletions,
42 insertions), but p95 ready delay was 5.44 s and maximum delay 6.00 s. No final
windows were dropped; queue p95 was 2.93 s. Its peak server working set was
468,389,888 bytes (about 447 MiB). This evaluates the actual small profile, with
440 ms endpointing/minimum speech 220 ms versus base's 400/180 ms, so it is not
a strictly model-only comparison. The clear speed/accuracy tradeoff means it is
an opt-in evaluation option, not the new production default.

The standalone batch harness now uses fresh output directories (`--output-dir`
can select one) and refuses to overwrite earlier evidence. Its model path follows
the moved compact asset. Old reports' original tool-directory paths are historical.

### Context-window experiment and decision

The base-model experiment changed both endpoint silence (400 → 800 ms) and
maximum utterance duration (6.5 → 12 s). It reduced WER to 35.19%: 114
substitutions, 16 deletions and 28 insertions. All 31 final windows returned text,
with no final drops or inference errors. This does not isolate which of the two
timing changes caused the improvement. The harness exposes `-EndpointMs` and
`-WindowMs` explicitly and records them; production defaults are unchanged.

Post-voice latency alone hides an important regression. The first finalized
Genesis 49 reference appeared at these elapsed replay times:

| Configuration | Final transcript containing reference |
|---|---:|
| Base, current endpoints | 37.761 s |
| Small Q5_1, current small endpoints | 39.571 s |
| Base, longer windows | 42.950 s |

The longer-window candidate delayed that transcript by 5.189 s versus current
base even though its p95 ready-after-last-voice metric remained below two seconds.
These are observed transcript completion times, not successful scripture detection
or manually timestamped semantic endpoints. All configurations still hit the
known plural/range parser gap. Preserve provisional evidence and measure reference
commit time separately when implementing rolling context; do not adopt longer
finalization waits as a blanket live-performance fix.

The two follow-up full replays passed without inference errors. Existing 19
audio checks, 269 parser checks and six scorer checks passed again, and the updated
evaluation executable compiled successfully. Its build fingerprint is retained
in `runs/phase2-small-live/build.json`. No live microphone/EasyWorship interaction
was performed as part of these headless checks.

- Empty recognition prompt; ScriptureDetector remains separate from literal speech.
- Reject long near-total repeated-output loops (at least 40 words, at least eight
  repeats of a one-to-six-word pattern, covering at least 80% of output). This is
  a conservative text safeguard, not a substitute for an acoustic classifier.
  It can still reject a genuinely very long repetitive utterance; validate more
  worship/call-and-response samples. Short emphatic repeats remain unchanged.
- Optional neural filtering exists for controlled evaluation but is disabled by
  default because its current settings failed the speech-quality comparison.
- Evaluation window title identifies Gazely; the complete interface redesign is
  still a later milestone, not delivered by this title change.

The prompt-only full replay predates the loop guard; the VAD replay includes it.
The final configuration is additionally exercised by `runs/phase2-final-smoke`.
Repetition behavior is tested directly with the reported Nephi pattern, long
phrase loops, normal references, distinct long speech and short emphasis.

## Validation and build

- 19 audio/text-safeguard checks and 269 existing parser checks passed.
- Six WER-scoring checks passed.
- Silence, seeded synthetic noise and steady hum each returned no transcription
  with the experimental neural filter: [test report](runs/phase2-synthetic-noise/synthetic-noise.tsv).
- Both full real-time comparisons completed without inference errors.
- Evaluation executable compiles successfully using the existing offline builder.

The old controller was running, so its executable was not replaced. The test build
is `EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/Gazely-Phase2.exe`. Close the old
controller before starting it; use Local AI, Balanced, Normal pickup, Automatic
Live off for recognition evaluation. Existing RUN_CONTROLLER.bat still opens the
old baseline until the canonical executable is deliberately updated. The new
build uses the existing models/runtime/version map and diagnostic log.

A local rollback source/executable snapshot is in ignored
`benchmarks/tools/baseline-v6.3/`. No EasyWorship automation was changed, and the
new build has not yet been manually exercised with the user's live microphone
or EasyWorship session.

## Next work within Phase 2

1. Compare larger compressed recognition models and context-preserving window
   strategies through the live pipeline; batch gains alone do not select a default.
2. Tune neural filtering against actual noise and quiet speech rather than enabling
   a setting that loses speech accuracy. Keep literal transcript and inference apart.
3. Evaluate overlap reconciliation and stable partial/final commits, with tests
   for legitimate repetitions, corrections and explicit reference boundaries.
4. Add timestamped labels, held-out speakers and ordinary 8 GB hardware runs.

Do not mark Phase 2 complete at 40.31% WER. Phase 3 still needs plural/range
references and contextual navigation; those explain the zero-detection results
even when the Genesis reference is transcribed correctly. Quote retrieval and
the new interface/integration Settings also remain planned work.
