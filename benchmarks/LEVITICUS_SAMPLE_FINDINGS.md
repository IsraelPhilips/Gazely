# Leviticus selection and audio context investigation

6 October 2026. Source: user-supplied Stewards Meeting - 20th September 2026.mp3.
Extracted source minutes 13:00–16:00 without changing the original. The Leviticus
call occurs near 15:15–15:24 in this extraction's source timeline.

Local evidence, ignored by Git:

- `runs/leviticus-source/decoded.wav`: 180 s, PCM16 mono 16 kHz.
- `runs/leviticus-base-intact`: base.en CLI batch decoding; contains “Leviticus
  says, 6th verse 13” and later verse 11/12 instructions. Batch processing ~14 s.
- `runs/leviticus-base-live`: production 6.5-second maximum windows, real-time
  replay. The reference-number phrase was absent from the final chunk; “go to
  verse 11” survived later. No conclusion about the original session's exact
  Deuteronomy transition is possible without its diagnostic log.
- `runs/leviticus-base-context`: same base model and server, experimental 16-second
  maximum windows and 1-second endpoint, accelerated replay. The reference became
  “Let's see what Leviticus says. 6th verse 13.” Verse 11 still became “West 11.”

The experimental long-window run is a text-quality probe, not live latency evidence.
Its start overlapped the tail of the baseline run; do not compare timing as an
isolated performance benchmark. The replay harness appends only 600 ms EOF silence,
less than this experiment's 1-second endpoint, so its final audio tail was not
fully flushed. No complete WER ground truth was supplied. No ASR model, prompt or
production endpoint/window defaults changed.

## Implemented interpretation fixes

- Explicit “go to / turn to / open / book of” requests can retain a book across
  brief filler and join a chapter-plus-verse call within 15 seconds. New book names
  take precedence. Existing pending-reference expiry/ordinary-speech tests remain.
- Chapter-slot ordinals such as “6th verse 13” and “sixth verse thirteen” are
  recognized without converting unrelated ordinal words in ordinary speech.
- “Leviticus says 6th verse 13” parses after the known book.
- “Go to verse 11”, “start from verse 11” and “okay, verse 12” work within the
  existing chapter context, including the tested embedded command forms.
- Recent explicit/contextual/navigation selections receive a 90-second protection
  from quotation-driven main-card replacement. Quotes still enter Detections.
  This does not block a newly parsed reference/verse instruction or a manual action.
  Book-only retrieval scope now restricts quote-driven selection to that book even
  before its chapter is known. This is not an indefinite scripture lock.

Tests: the parser suite includes the reported split phrase and the actual longer-
window transcript, missing/expired anchors and ordinary ordinal speech negatives.
The full UI suite at `runs/ui-leviticus-selection` verifies 6:13 → 6:11 → 6:12,
rejects a competing Deuteronomy quote after the former ten-second protection would
have expired, and rejects a current-revision 6:13 quote after verse 11. It also
retains the earlier Micah, Isaiah, range, navigation, history and preference tests.
These are text/UI tests using a fake adapter, not a claim that the audio decoder
correctly recognized all those words.

## Acoustic next step

The same-server experiment supports testing selective context-rich re-decoding of
uncertain calls. It does not justify globally increasing latency or translating
“west” to “verse.” A production recovery path still needs bounded audio retention,
trigger selection, queue priority/cancellation, stale-result handling and measured
false-reference rates before enabling it. No such automatic second decoder is
enabled by this change.
