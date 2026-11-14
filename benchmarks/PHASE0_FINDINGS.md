# Phase 0 — discourse-cue survival and latency baseline

14 November 2026. Measurement only; no product code was changed.

Phase 0 existed to test one assumption before building the scripture-plan layer:
does the wording it depends on actually survive our ASR? If enumerations and
ordinals never transcribe, the plan would be reasoning over sentences that never
arrive.

## Part A — do discourse cues survive ASR?

Method: searched existing, already-transcribed real sermon output for the cue
families the plan depends on. No new inference was run; this reads retained
evidence.

| Source | Lines |
|---|---:|
| `runs/mvp-soak-rerun-20261015/transcript.txt` (30-minute paced replay) | 446 |
| `runs/leviticus-base-live/transcript.csv` | 52 |
| `runs/phase1-sermon-realtime/transcript.csv` | 52 |
| `2026-09-30-sermon/base-pcm-no-prompt.txt` | 27 |
| **Total utterances scanned** | **556** |

| Cue family | Matches |
|---|---:|
| directive — `(go\|turn\|open\|jump) to` | 9 |
| directive — `let's (start\|go\|turn\|read\|jump\|look)` | 4 |
| deictic — `(next\|previous) verse` | 4 |
| anaphoric — `that <book> that (we\|i) (started\|called\|mentioned\|read)` | 2 |
| anaphoric — `we started with` | 2 |
| ordinal — `(first\|second\|final\|last\|another\|other) (scripture\|one\|verse)` | 0 |
| **Total cue-bearing utterances** | **21 of 556 (≈3.8%)** |

**Verdict: the cues survive, and the hardest pattern survives intact.**

The decisive observation is a real transcript line:

> `and if you go to go back to that romans that we started with you are on fire for god`

That is anaphoric reference to a previously named book, transcribed cleanly —
exactly the capability the scripture plan needs. Directive phrasing
("let's start from verse eleven", "go to Leviticus") also survives repeatedly.

### Honest qualifications

- **Not independent recordings.** The 240-second sermon appears twice
  (`phase1-sermon-realtime` and the `base-pcm-no-prompt` file). This is 4
  transcripts covering roughly 3 distinct recordings, not 4 independent preachers.
- **Ordinals were not observed at all.** The 0-count is a crude regex proxy, not
  evidence that preachers never say "the final scripture". This cue family has no
  real fixture yet and must not be treated as validated.
- **Enumeration was rare.** Only 3 occurrences of "scripture(s)" in 446 lines.
  The example that motivated this work ("we'd be reading 3 scriptures…") was not
  observed in this sample. The plan's enumeration path remains unproven on real
  audio and needs a fixture from an actual service.
- Cue density (~3.8% of utterances) is a low-frequency feature. That supports
  building it, but also means the plan cannot be the headline capability — the
  common case stays explicit single references.

## Part B — latency baseline

Source: `2026-09-30-sermon/controller-timing.csv`, 39 real per-utterance samples.

| Stage | min | median | p95 | max |
|---|---:|---:|---:|---:|
| Inference (`InferenceMs`) | 0 | **1,245** | 2,676 | 3,654 |
| Ready — last voiced frame → detection (`ReadyMs`) | 1,006 | **1,629** | 2,919 | 4,678 |
| Queue + dispatch (`ReadyMs − InferenceMs`) | 3 | 243 | 1,460 | 1,928 |
| EasyWorship automation dead time (from source constants) | ~1,100 | ~1,300 | ~1,500 | — |

`ReadyMs` starts at the last energy-classified voiced frame. It excludes WinForms
dispatch and all EasyWorship automation, so it is not the audience-visible delay.

Approximate end-to-end, detection-to-live on this data:

```
total ≈ ReadyMs + automation dead time
      ≈ median 1.63 s + 1.3 s  ≈ 2.9 s
      ≈ p95    2.92 s + 1.5 s  ≈ 4.4 s
```

Automation dead time is derived from the fixed constants
(`ForegroundDelayMs=180`, `ClickDelayMs=180 ×2`, `FieldDelayMs=110 ×5`, plus up
to 60 version keys at 8–15 ms to re-anchor), not measured against a running
EasyWorship — which this session cannot observe.

## What Phase 0 establishes for Phase 1

1. **The plan layer is viable.** Its input wording survives ASR; it is not
   reasoning over text that never arrives. Proceed.
2. **Automation dead time is a guaranteed ~1.1–1.5 s win** and the single largest
   addressable lever, ahead of anything model-related. This is the Phase 1 target.
3. **"Faster" now has a number.** Phase 1's claim must be measured against the
   figures in Part B, not against a feeling.
4. **Two fixture gaps are open**, and must be closed before the plan layer is
   claimed to work: a real enumeration utterance and a real ordinal
   ("the final scripture") utterance.

No models, downloads or runtime packages were involved. Nothing was rebuilt.
