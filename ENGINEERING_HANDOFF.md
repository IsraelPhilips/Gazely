# Gazely engineering handoff

Current baseline: v6.3 prototype. Consolidated 30 September 2026.

[Product vision](PRODUCT_VISION.md) governs product scope and constraints.
[Brand guidelines](Gazely_Brand_Guidelines.md) governs identity. This document
records implementation and evidence; proposed architecture is not a claim of
shipped functionality.

## Immediate engineering reality

### Remembered preferences and stable detections — 2 October 2026

`WorkspacePreferences` stores theme, automation toggles, speech engine/model,
voice pickup, input display name and early-result toggle as escaped key/value text
beside the executable (`gazely-preferences.txt`). Writes use temporary-file replace;
invalid/missing values retain defaults and unavailable devices retain the available
selection. Errors are logged. Restore bypasses the enable-Automatic-Live confirmation
for the user's already saved choice, but never starts capture or sends output.
Preferences save on change and before close. Tests use an isolated path and cannot
overwrite the operator's settings. Session transcript/history is not persisted.

The detections renderer now caches cards by history item plus evidence fingerprint,
retains unchanged control instances, and replaces only changed/off-page cards.
A double-buffered panel batches child/layout updates under WM_SETREDRAW, restoring
redraw in finally. Unchanged rows are no longer recreated/rethemed every refresh.
Focus/menu deferral, snapshot actions, pagination and scrolling remain intact.
Regression testing also found and fixed a timing guard that incorrectly treated a
recovered quote as a recent explicit reference, blocking a following quote update.

`benchmarks/runs/ui-preferences-stable-list-v2` records the passing complete UI
suite: unchanged-card identity, preference save/reopen (including Automatic Live),
capture remaining stopped, and the earlier Isaiah/range/navigation/history checks.
Tests use a fake presentation adapter; no real projection actions were sent.

### Sermon follow-up — 2 October 2026

Preserved ASR settings/models and the existing global lexical/semantic thresholds.
User text fixtures exposed parser/UI problems independently of acoustic quality:
chapter-only calls with trailing speech were discarded; an unresolved draft could
overwrite a recovered quotation; a populated Selected passage blocked later quotes;
and broad context could keep selecting the preceding verse.

Chapter prefixes now retain a non-presentable draft (e.g. Isaiah 51:…). Speech
establishes a 90-second book/chapter retrieval scope, reset on Stop or clearing
detections. Strict lexical matching can search that chapter; there is no relaxed
global quote threshold. Meaning search first uses current speech. It makes at most
one additional continuation query (last two chunks, capped at 40 words) when no
candidate exists, or when a recent named chapter has no current-chunk match. The
broader lexical window is retained. Main-card suggestions favor the named chapter
and respect operator hold and current revision; an unresolved draft no longer
erases an already recovered passage. Suggestions outside the scope can still enter
the review panel. Scope is context, not a verified live-screen observation.

`SpeechPresentationPolicy` projects explicit spoken ranges to their first verse
without altering the parser's stored range or manually entered ranges. Clear
explicit references now qualify at heuristic score .88 (formerly .90). Clear
next/previous-verse instructions qualify when their result is adjacent to the
recent unambiguous selected speech reference; previous explicit translation carries
forward. Corrections, number guesses, quotes/paraphrases and ambiguous navigation
remain review-only. A bare “go” is not treated as an advance instruction. This adds
no read-the-whole-verse waiting period. Automatic Live remains opt-in; sent output
is still unverified. Parser context expiry remains a limitation for long pauses.

Detections Clear uses a default-No confirmation, preserves History and advances an
epoch to reject queued pre-clear results. New quote keys include that epoch so
future mentions can reappear. Cards separate KJV text (larger serif) from timestamp,
match type and smaller Heard evidence. These supersede earlier layout notes.

Validation: 330 existing parser cases and original lexical/multi-quote fixtures
pass. New tests cover range-first policy, clear numeric calls, anchored navigation,
retained chapter drafts and review-only quotations. UI replay verifies the user's
distorted Matthew tail reaches Detections, and the supplied Isaiah readings move
Selected passage to 51:1 then 51:2. Auto prepare sends Genesis 49:3 then 49:4 via a
fake adapter. Clear/history/epoch, held selection, pagination and icon actions also
pass. Evidence: `benchmarks/runs/ui-isaiah-replay-v3` and
`benchmarks/semantic-reported-sermons-v2.csv`. The latter measured ~1.1 s worker
startup, 0.47 s p95 query and 467 MiB peak worker memory; it is not an end-to-end
audio benchmark. The stand-alone Isaiah tail incorrectly matches Isaiah 48:15;
the paired reading retrieves 51:2. The ordinary library anecdote still returns a
false suggestion (2 Kings 4:11). This is why global semantic suggestions remain
review-only. No accurate transcript/audio for the second recording was supplied;
do not claim WER or faithful reconstruction from its ASR text.

### Segmented workspace UI — 1 October 2026

The live workspace now uses the full window width. At >=1400 px, Live transcript,
Detections and Selected passage occupy independent full-height columns. Narrower
windows retain a full-height detection column with transcript and passage stacked
beside it. Compact reference editing temporarily enlarges the passage area and
restores the transcript when closed. Very small windows scroll rather than clipping
the controls. Secondary pages retain their centered frame.

Reference inputs are behind Edit reference; the immediate Present and Prepare
actions remain visible. Vector-drawn play/prepare/info/menu icons have text names
and tooltips. Candidate menus contain Pin/Dismiss; the section menu contains All
history and Reset version tracking. Open candidate menus defer incoming row refresh.
The selected passage has an accent edge, cards have subtle boundaries, and manual
Present uses the same icon treatment. Existing engine, parsing and adapter handlers
are unchanged. No model or retrieval changes are part of this UI increment.

`Test-Interface.ps1` now checks the adaptive panes, editor disclosure, menu-based
reset, direct Present through a fake adapter, pin/dismiss retention, expanded-view
return and icon action snapshots. Dark 1366x768 evidence is in
`benchmarks/runs/ui-segmented-verified`; the suite also resizes through 1920x1080,
1280x980 and 980x700. Light-theme render evidence is retained in
`benchmarks/runs/ui-segmented-release-light` (1280x980, full suite passed).
This supersedes the older toolbar/button placement notes below. Broad high-DPI
and assistive-technology validation remains outstanding.

### Offline candidate search increment — 1 October 2026

Implemented a session-persistent Live workspace candidate panel: 20 rows per page,
Find, Pin, Dismiss, Expand list, direct Prepare/Present and retained evidence/history.
Rows use immutable detection snapshots for actions; incoming results defer visual
reordering while the operator is interacting. Dismissed entries remain in History.
History is in memory for this app session, not persisted across application restarts.
Uncertain but valid references and quotes require operator review; incomplete
references offer Complete / edit. New spoken references default to KJV unless a
version was explicitly requested. Actual adapter outcomes are recorded as attempts,
not proof of verified live output.

Quotation jobs now retain multiple results from a transcript, independently parse
multiple named references, and preserve late results in the candidate panel without
overwriting a held main card. Lexical context retains up to 12 chunks / 120 words /
30 seconds and expires after a 15-second gap. Search jobs run serially off the UI
thread. Sustained queue latency and long-service memory still need measurement.

Optional offline semantic worker: `SemanticSearch/Program.cs` targets .NET 8 and
uses ONNX Runtime 1.20.1. The bundled self-contained win-x64 runtime needs no Python
or user SDK. Pinned quantized BGE-small-en-v1.5 weights (34.0 MB) and MiniLM-L6-v2
cross-encoder weights (23.2 MB) search 31,102 KJV and 31,095 WEB verse documents.
Top 32 document variants are reranked and grouped by canonical KJV reference;
up to three results pass cosine >= .55 and reranker logit >= 0. These thresholds
are experimental gates, not calibrated probabilities. Every semantic result has
RequiresReview set and cannot trigger Automatic Live. No Qwen was downloaded.
An optional E5 comparison download failed twice; no E5 results or installed-model
claims should be inferred from the worker's experimental E5 support.

`SETUP_SCRIPTURE_SEARCH.ps1` pins revisions and SHA256 hashes, publishes the worker
and builds corpus/model/vocabulary-bound indexes. It is a developer setup script
(SDK needed to build), not a finished user installer. `Import-ModernSearchCorpus.ps1`
creates the public-domain WEB retrieval corpus and provenance; output remains KJV.
Runtime, model, index and corpus folders total approximately 252.5 MB / 241 MiB,
excluding speech recognition and development dependencies. Missing/failing semantic
search falls back to lexical lookup; failure retries are limited to once per minute.

Evidence in `benchmarks/semantic-modern-results.csv`: 2/2 literal fixtures and 6/11
paraphrases found the intended verse, 5/5 negative fixtures abstained. In the separate
author-written text set `semantic-heldout-results.csv`, 14/15 intended references
were found and 8/8 negative fixtures abstained. Additional incorrect/loosely related
cards occur; recall is not precision. Measured search-worker startup ~1.7 s, query
p95 0.51–0.56 s, peak worker memory ~447–481 MiB on this development machine. These
exclude ASR, UI, queue time and EasyWorship; they do not establish live latency or
8 GB readiness. Hard cases such as a heavily paraphrased Psalm 23:1 remain missed.

Final regression evidence: build succeeds; 330 parser cases, 19 original quotation
fixtures plus 3/10-quote bursts, multiple-reference aliases, context limits, 20 fake
adapter lifecycle checks and 19 version-navigation checks pass. `Test-Interface.ps1`
verifies asynchronous late quotations, operator hold, 25-candidate pagination/search,
focus-stable arrivals, expanded panel and immediate exact-row KJV Present through a
fake adapter. Seven-page centering and render evidence is in
`benchmarks/runs/ui-candidates-release`. No real live commands or microphone access
were used. Broad DPI/accessibility, real-sermon semantic precision, service-length
queue behavior and target-machine tests remain open.

Older dated implementation notes below describe preceding increments; this section
supersedes their lexical-only and latest-12-row descriptions.

Direct-present UX, 1 October: removed the per-send Yes/No dialogs in RunDetected
and RunScriptureAction. Clicking Present is the operator's approval, including
reviewed quotations/history; automatic-live eligibility remains unchanged. Invalid
references, missing mappings and adapter failures retain their existing handling.
Live workspace now exposes Reset version tracking beside All history, calling the
same reset method as Present (not PerformClick on a hidden control). Its enabled
state follows the existing busy-state reset button. Test-Interface verifies that
the visible workspace action invalidates a seeded selection while Present is hidden.

Read-only EasyWorship inspection, 1 October: the current Windows accessibility
snapshot exposes the reference edit (Leviticus 10:2) but no named Bible-version
rows, selection list or selected-version value. A visual inspection shows KJV in
the library translation column and the existing live title; the selected Bible
row itself is outside the small visible list viewport. The live title is not a
valid substitute for the library selection, since selecting another Bible need
not update live output. No version keys, clicks or live commands were sent.
EasyWorship was brought to the foreground for visual inspection. Automatic manual
version detection is NOT implemented or verified by this inspection. The current
computer-use snapshot does not provide a direct MSAA/selection-pattern probe;
absence from its tree does not prove every native accessibility route is absent.
Next validation: compare snapshots after an operator's manual selection, then
evaluate a dedicated adapter readback implementation (UIA selection and legacy
accessibility), with unknown as an explicit result. A visual fallback must be
restricted to library selection/translation evidence, measured for latency, and
must not silently infer selection from the currently live slide. Preserve the
existing fast path pending a trustworthy signal; manual reset remains necessary.
The detailed next quotation-recognition sequence is maintained in EXECUTION_PLAN.md
under Milestone 4 rather than a separate plan document.

Manual MSG comparison completed: the operator selected MSG, and visual inspection
confirmed the highlighted MSG library row and MSG translation column. The live
slide also showed MSG at this observation; no command was sent by this inspection.
Comparing accessibility snapshots exposed only a reference-count change from
31102 to 31015 and a toolbar label change to Live. Neither snapshot supplied a
selected-version name/value. Reference count must NOT be used as a version ID:
counts can coincide between translations and may depend on library state. This
confirms the current snapshot path cannot reliably identify the selected version.
A cropped visual reader has a visible source to investigate (library row or
translation column), but remains unimplemented and unvalidated for offscreen rows,
occlusion, DPI, themes and minimized windows. Do not claim automatic detection is
working or remove the manual-reset guidance on the basis of this comparison.

1 October version-navigation follow-up: BibleVersionPosition seeds the configured
KJV index on first process attachment (PID plus process start time), per the user's
startup-default requirement. A matching request returns before finding/focusing
the version list. Listening cancellation and adapter enable/disable no longer
clear the EasyWorship selection. Navigation invalidates tracking before focus or
keys and commits only after settling; interrupted moves, explicit reset and map
changes remain unknown and recover through the existing anchor path. A new process
seeds KJV again. Startup selection and externally changed selections are not read
back. Test-VersionNavigation.ps1 passes 19 production-state checks, and
Test-PresentationAdapters.ps1 passes 20 lifecycle checks; neither sends live output.
Actual EasyWorship switching still needs operator verification after restarting.

Alignment follow-up (screenshots 19/20): all seven sections now mount their content
through MountCenteredPage, sharing a 1440px maximum width and 28px inner gutter
with Live workspace and the masthead. Present/Speech setup distribute main and aside
cards proportionally on wide windows and wrap on smaller ones; their inputs retain
compact widths. The live Use edit action aligns vertically with its input frames,
and recent-history text and action buttons align within each row. Regression checks
assert symmetric page margins on every section at 1920x1080, edit/input alignment,
and the existing resize, history, dropdown and engine-selector safeguards. Light and
dark evidence: benchmarks/runs/ui-shared-alignment-first and ui-shared-alignment-dark.

Resize follow-up (screenshots 17/18): removed the TabControl's cached Region, which
could clip newly enlarged content at the old width. Its native border is now cropped
by a resizing parent panel, without a window region. Live cards explicitly remeasure
their scroll viewport after a resize, clearing stale scrollbars first. The live
workspace and masthead are centered at a maximum content width of 1440 pixels;
listening controls sit beside the heading and the meter minimum size matches its
assigned space. The book editor is now an editable ComboBox populated from the
canonical 66-book list, with native SuggestAppend autocomplete and list selection.
Free text still passes the existing book/chapter/verse validation and operator hold.
Autocomplete is prefix-based, not fuzzy scripture interpretation.

New UI regressions expand a shown form to 1920x1080, restore through 1280x980 and
980x700, and assert no clipping region, transcript overflow, audio-control overlap,
editor overflow or needless card scrollbar in tall windows. Book-list completeness,
selection of Titus 2:11 and invalid-book rejection pass. Dark/light renders are in
benchmarks/runs/ui-resize-book-reflow and ui-resize-book-release-dark. This is a
native adaptation of the approved mockup; native combo/scrollbar details are not
pixel-identical to its browser rendering. Broader Windows DPI coverage remains open.

The approved Gazely mockup has been adapted to native WinForms across all seven
pages, without a browser runtime. Light is the initial theme; Light / dark remains
available. Field wrappers preserve the existing TextBox/ComboBox instances and
events, while constraining widths and providing rounded focus borders. Soft cards,
compact navigation, rounded buttons and switches are shared across the pages.
Present and Live workspace show locally retrieved KJV excerpts, explicitly labeled
KJV regardless of the selected adapter output translation. Full evidence remains
available separately; long excerpts may be ellipsized.

Recent history is pinned beneath the live cards (latest 12 rows); smaller windows
scroll the cards independently. Each final valid row offers Prepare/Present.
Selecting history uses a snapshot, forces review, does not duplicate the history
entry and holds the editor against incoming speech. Provisional, unconfirmed and
invalid references cannot be sent from history. The full History page has the same
review/action path. Speech continues recording suggestions while a reference is
held. All actions reuse RunDetected and the existing presentation coordinator.
No action labels claim verified live output. History remains session-only.

Validation: 330 parser cases, 19 quotation cases plus preview retrieval checks,
19 audio pipeline checks and 20 fake-adapter lifecycle checks passed. The actual
WinForms harness checks navigation fit/actions, both engine selectors, input widths,
split references, protected operator edits, ranges, invalid verses, history snapshot
isolation and presentation eligibility. Dark 980x700 and light 1280x980 renders
were inspected. Latest evidence: benchmarks/runs/approved-ui-final and
benchmarks/runs/approved-ui-pinned-history. Tests did not send to EasyWorship or
capture microphone audio. Real live-presentation and broad DPI/accessibility testing
remain necessary; these checks do not establish that every possible workflow is bug-free.
The installed-voice pronunciation/UI run failed the book-only draft assertion once;
the initial failure lacked field values, so its cause is unconfirmed. Two subsequent
full runs (light 1280x980 and dark 980x700) passed, including the user's pronunciation
fixture. Failure diagnostics now report the actual book, reference, enabled state and
operator hold. Keep this intermittent check open; the 20-second waiting-card timer
is a possible timing confound, not a proven diagnosis. Evidence is retained in
benchmarks/runs/approved-ui-pronunciation-recheck and approved-ui-pronunciation-small.

The scripture card now opens an incomplete draft on a finalized book-only cue.
The existing 15-second parser continuation window remains; the waiting card expires
after 20 seconds unless the operator has edited it. Book, chapter and verse/range
inputs support manual completion. Editing holds the card against incoming speech;
Use validates against the Bible canon and creates a review-required operator entry.
Follow speech releases the hold and resets reference context. Recognition continues
transcribing while held. No edit automatically presents. Rounded buttons/card borders
and keyboard-operable on/off switches use the Gazely palette. Smaller windows scroll
the scripture card. UI regressions cover bare books, split calls, operator priority,
valid ranges and invalid verses; dark/light rendering inspected. Speech accuracy is
unchanged by this interface work. The builder clears obsolete pending updates after
a successful direct Gazely.exe replacement to prevent launcher downgrades.

Review-only chapter pronunciation recovery is implemented in
`ChapterPronunciationRecovery.cs`. It uses an installed English Windows synthesis
voice's phoneme events with output discarded, caches spoken chapter numbers 1–150,
and compares only the alphabetic slot between a recognized canonical book + chapter
and a verse cue. Candidates are limited to that book's chapter count. Normalized
phoneme edit distance must be <=0.25 with >=0.08 margin to the runner-up and at
least six phoneme events. No phrase-specific substitution for “for tonight” exists.
All complete recovered references require review; conflicting numeric verse tokens
produce a chapter-only hint with presentation disabled. Literal transcript is kept.

The supplied “Genesis chapter for tonight, verse 8, 3, and verse 4” now renders
“Possible Genesis 49” / “Verse unclear”. The UI callback and disabled presentation
were verified offscreen in `benchmarks/runs/ui-pronunciation-verified`.
`Test-ChapterRecovery.ps1` passes nine cases on Microsoft David Desktop, including
“twenty tree”, ordinary speech, nonmatching phrases, book limits and invalid verses.
Initialization measured 745 ms; the nine checks took 53 ms on this machine. These
small text/pronunciation fixtures are not real-audio or held-out recovery accuracy.
This feature neither re-decodes audio nor performs general semantic reasoning.
Other voices/languages remain unvalidated; unavailable pronunciation support leaves
the unresolved-book card visible and records the limitation in Diagnostics.

Unresolved explicit book/chapter cues now remain visible for up to 20 seconds,
appear in History with no invented numeric confidence, and clear on resolution,
expiry or reset. Parser tests now cover 330 cases. Provisional parsing preserves
this state. When the current executable is open, the builder stages
`Gazely.pending.exe`; the launcher activates it after the user closes Gazely and
retains `Gazely.previous.exe` for rollback. It does not terminate the running app.

Current executable: `Gazely.exe`; `RUN_GAZELY.bat` launches it. The prior
`Gazely-Development.exe` was running during this update and was left untouched.
The seven-page shell now includes session-only suggestion History with time,
reference, percentage match score, evidence type, provisional/final/review status
and source text. Provisional revisions share a row; an unconfirmed final marks
the row Not confirmed. Entries snapshot their detection evidence. Explicit scores
use parser rule strength; quotation scores use the lexical retrieval score rather
than the fixed 0.75 action-policy confidence. Neither is a calibrated probability.

The main workspace replaces the flickering voice/no-voice badge with an input
level meter driven by the same RMS/peak updates as Speech setup. Provisional text
is labelled Live text (revising). No recognition/endpoint timing was changed.
Quotation retrieval is still lexical over up to three adjacent final segments
within a 15-second gap, with conservative word coverage/order and ambiguity checks;
it is not semantic paraphrase retrieval and may miss heavily garbled quotations.

`Package-BrandAssets.ps1` copies the approved PNG symbol unchanged and packages
the supplied 16–256 px app PNGs in an ICO container. The build uses CodeDOM without
loading/locking its output and embeds that icon in the executable. The header and
window icon use these assets; no SVG wordmark/font substitution was performed.
Rendered history and UI regression evidence is in
`benchmarks/runs/ui-history-verified`. Parser (324), quotation (19) and adapter
lifecycle (20) checks pass; the UI test verifies history snapshots/revisions and
the reported split-reference path.

User-reported split-reference regression fixed: “In Genesis chapter” followed
seven seconds later by “Chapter 49 verse 3 and verse 4 …” now yields Genesis
49:3–4. Previously the chapter keyword prevented pending-book retention, and
the six-second pending timeout was shorter than the observed chunk interval.
Explicit unfinished book/chapter calls now accept the next numeric/chapter
continuation within 15 seconds; intervening unrelated speech clears them.
Combined literal source evidence survives preview parsing. 324 reference cases
pass, including expiry, unrelated speech, new-book replacement and split calls.
The real OnFinalTranscript UI callback was tested with a simulated seven-second
gap and the scripture panel rendered Genesis 49:3–4; see
`benchmarks/runs/ui-split-reference-fix/split-reference-regression.png`.
This test uses supplied text, not a fresh audio recognition run.

Adapter/UI follow-up: `PresentationAdapters.cs` defines typed intents, capability
flags, a presentation adapter contract, result states and a coordinator. The
EasyWorship 7 wrapper preserves existing automation, restricts discovery to a
version-7 executable and reports prepared versus sent-but-unverified results.
The last 256 attempted intent IDs are deduplicated, including failed/ambiguous
attempts; unsupported operations are rejected. A generation invalidates pending
commands on Stop/disable/re-enable. Cancellable waits check every 25 ms and before
Enter. Commands execute on a separate STA thread; no live readback is claimed.
Native SendKeys/UI Automation calls cannot be interrupted mid-call, and already
sent commands cannot be recalled. Twenty fake-adapter lifecycle tests pass;
real EasyWorship range, cancellation and STA behavior still require validation.

Settings/Integrations enables the bundled EW7 adapter, persists the choice in
`%LOCALAPPDATA%/Gazely/integrations.txt`, reconnects by probing, and shows planned
adapters as unavailable. There is one implemented destination. Adapter downloads,
multiple implemented destinations, secure API credentials and package rollback
remain future implementation work; the settings page does not simulate them.

`GazelyInterface.cs` reuses the engine controls in a new six-page dark/light
WinForms shell: Live workspace, Present, Speech setup, Integrations, Bible
versions and Diagnostics. It uses Gazely Black/Porcelain/Signal Lime, separate
transcript/evidence surfaces, a full evidence dialog and result status. Segoe UI
is a system fallback pending approved font/artwork packaging. `Test-Interface.ps1`
renders actual controls offscreen, with a clearly synthetic quotation fixture and
no microphone/presentation actions. Compact 980x700 and 1240x850 light layouts
have been inspected; these are not DPI or complete accessibility checks. The
full onboarding/download/recovery experience remains unfinished.

Final render evidence is retained in `benchmarks/runs/ui-gazely-final-dark` and
`ui-gazely-final-light`; superseded blank/intermediate render folders were removed.
The compact check exposed and fixed clipped map actions and LF-only map text;
the editor now displays normalized CRLF without changing the stored map on load.
Escape stops capture, cancels pending adapter commands and disarms automation.

The build script combines all three C# source files. `RUN_GAZELY.bat` opens the
current development executable; existing baseline launchers remain available.

Latest source builds `Gazely-Development.exe` separately from the existing
baseline and Phase 2 binaries. It adds ranges, adjacent-segment range completion,
corrections, canonical next/previous verse navigation and final-event deduplication.
307 parser cases plus range-policy/deduplication assertions and 19 audio checks
pass. Navigation/corrections require operator review. `SetDisplayedContext` is
tested but is not yet connected to authoritative adapter live-state readback;
do not treat detected or prepared passages as verified live passages.

The full real-time `benchmarks/runs/phase3-reference-live` recording produced
Genesis 49:3–4 and then Genesis 49:4, 52 final results, zero dropped finals/errors,
40.09% WER and 1.72 s p95 ready-from-last-energy-voice delay. This delay is not
timestamped semantic-reference latency and excludes UI/presentation operations.

`ScriptureQuoteIndex` adds offline lexical retrieval over `corpus/kjv.tsv`.
The source archive, hashes and rights notice are recorded alongside the corpus;
the UK KJV rights exception still needs distribution review. Index construction
and lookups run off the UI thread. Session/revision guards discard stale results;
Clear resets pending context. Up to three adjacent final phrases (15 s gap limit)
support split quotations. The matched KJV text appears in the evidence label and
hover tooltip; it does not change the selected presentation translation. All
quotation suggestions require review and never automatically prepare/present.

`Test-QuotationIndex.ps1` passes 19 positive/negative fixtures plus split,
expiration, reset and bounded-context checks. Index load was about 0.5 s and max
fixture lookup about 90 ms on this machine, not a target-hardware performance
guarantee. Optional `-TranscriptCsv` reprocesses saved output without re-running
speech: small-model output yields Genesis 49:4 and Genesis 49:3 quotation
suggestions; base output yields none. Matthew 6:33 matches the corrected wording,
but not the garbled live output. No semantic/paraphrase engine is implemented.
Quotation tests are initial engineering fixtures, not calibrated accuracy claims.

[Phase 2 first delivery](benchmarks/PHASE2_PROGRESS.md): prompt removed, long
repetition guard added, optional neural filter implemented but default OFF after
it worsened WER. Current source builds the separate `Gazely-Phase2.exe` evaluation
artifact. `EasyWorshipController.exe` remains the old running baseline. Full live
replay WER improved 45.88% → 40.31% with prompt removal; Phase 2 is not complete.

[Next execution phase](EXECUTION_PLAN.md) is the active implementation plan,
including contextual navigation and proposed quality/resource gates. Milestones
there are planned work, not claims of implemented features.

Real-audio follow-up: [30 September sermon benchmark](benchmarks/2026-09-30-sermon/RESULTS.md).
Controlled replay reproduced Bible-prompt hallucinations. Unprompted quantized
small improves several phrases but has not reached the accuracy target; plural
verse ranges and quote retrieval were independently missing at that benchmark.
The current development changes above supersede those parser limitations. The report records
CPU throughput, memory, actual-session timing, limitations and next work.

The product owner compared both systems using the same audio. Gazely produced
major ordinary-speech errors and standalone Bible names that were never spoken;
Pewbeam captured almost all of the speech and was assessed by the user at roughly
98% accuracy. That outweighs the earlier synthetic successes. No accurate WER or
end-to-end latency comparison has yet been measured against the original recording.

The next task is live-pipeline replay and recognition hardening, not adding more
book-name substitutions or presenting parser confidence as recognition accuracy.
The Bible-heavy Whisper prompt caused hallucinations in the controlled sample;
it is not established as the sole cause of all live-session errors. Preserve
continuous speech context and inspect actual audio segments before selecting
the production model.
8 GB total-system RAM, presentation-app coexistence and download size constrain
model selection. Whisper Large V3 Turbo is a candidate, not an agreed dependency.

## Files and build

### Phase 1 replay tooling

First completed [real-time baseline and findings](benchmarks/runs/phase1-sermon-realtime/RESULTS.md):
52 finals, no dropped final windows, zero detections; median ready time 2.20 s,
p95 3.75 s from last energy-classified voice. The subsequent user-corrected
transcript gives 45.88% baseline WER; timestamped labels and held-out testing
remain pending. This baseline preserves behavior before Phase 2 changes.

`benchmarks/Run-LiveReplay.ps1` compiles the production `ScriptureDetection`
through `WhisperServerEngine` classes plus the exact `OnSpeechChunk` and
`WhisperWorkerLoop` methods into a temporary console executable. Source-boundary
and instrumentation checks fail visibly if those methods change. The harness
does not include `MainForm`, microphone capture or `EasyWorshipAutomation`.
Only telemetry is injected into the copied worker; the prompt, gain, queue
policy, final reuse and adaptive partial interval remain the production baseline.
The application source and executable are not modified by the replay tool.

`benchmarks/ReplayHarness.cs` feeds PCM16 mono 16 kHz WAV in 40 ms frames, with
the selected model's current endpoint settings and normal voice sensitivity.
Partials are enabled. An explicit 600 ms artificial silence tail flushes pending
speech at EOF and is recorded in the manifest. It uses the current Preview and
Feed/Suggest parser paths without UI dispatch or presentation actions.

Example from the project root (use a fresh output directory for each run):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File benchmarks/Run-LiveReplay.ps1 -Audio benchmarks/2026-09-30-sermon/decoded.wav -Mode realtime -OutputDirectory benchmarks/runs/my-replay -SaveChunks
```

`-Mode accelerated` drains the worker after each input frame for deterministic
coverage investigation; it does not simulate realistic queue pressure or live
latency. Parser context expiry still uses wall time, so accelerated results must
not stand in for real-time context tests. `-LimitSeconds 20` makes a short smoke
run. `-BuildOnly` compiles without opening audio or inference. Inputs must be WAV
in the stated format; `benchmarks/prepare_replay_audio.py` converts MP3 locally
using the pinned benchmark decoder, preserving the original. Python is developer
tooling only. The replay itself builds/runs with Windows PowerShell/.NET.

Each run retains source/audio fingerprints, server logs, event TSV, final
transcript CSV and summary JSON. New runs also retain harness/model fingerprints.
Optional segment WAVs stay local and are Git-ignored. Inference errors fail the
run and preserve diagnostics. Temporary executables are removed in `finally`.
Existing output directories are rejected to prevent overwriting evidence.

`benchmarks/Summarize-Replay.ps1` regenerates distributions and audio-interval
coverage from events. Ready timing starts at the last energy-classified voiced
frame, not the last word semantically needed for a reference. Coverage includes
pre-roll/overlap/padding and is not speech recall. Resource output separates
server and harness peak working sets; it does not measure a complete Gazely UI
plus EasyWorship deployment. Replay intentionally excludes UI-dispatch latency.

`benchmarks/score_transcript.py` computes whole-run word error rate only when
supplied a manually corrected transcript matching the replay interval. Its
normalization does not silently repair numbers/books; repeated committed words
count as insertions. Six scoring checks cover these cases. The user's corrected
reference and its qualifications are retained in `benchmarks/2026-09-30-sermon/`;
per-run `wer-user-reference.json` records reference/hypothesis hashes and scores.

### Existing application

The application remains in `EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/`; its
historical folder/source/executable names have not been migrated in this cleanup.

- `EasyWorshipController.cs`: single-file C# WinForms implementation.
- `Build-EasyWorshipController.ps1`: Windows PowerShell Add-Type build using resolved
  .NET Framework assembly paths. UTF-8 source reading; compile before replacement.
- `BUILD_AND_RUN.bat`, `RUN_CONTROLLER.bat`, `EasyWorshipController.exe`: canonical
  build/launch path. Current executable still identifies as Scripture Assistant v6.3.
- `SETUP_LOCAL_WHISPER.ps1` and model/setup/diagnostic BAT files: one-time downloads.
- `models/`, `whisper/runtime/`: installed runtime dependencies; preserve them.
- `version-map.txt`: church-specific Bible-version index mapping.
- `BIBLE_METADATA_SOURCE.txt`: metadata provenance and checksum; preserve it.
- `Test-ScriptureParser.ps1`, `Test-AudioPipeline.ps1`, `Test-SpeechPipeline.ps1`:
  reproducible checks; not disposable staging scripts.
- `scripture-diagnostics.log`: actual session evidence; retained despite cleanup.
- `evidence/speech-smoke-base.en.tsv`: retained historical synthetic test report.

No Python runtime, Visual Studio, NuGet or dotnet SDK is needed for the existing
builder. Required assemblies include System, System.Core, Drawing, Windows.Forms,
System.Speech, WindowsBase, UIAutomationClient and UIAutomationTypes. Do not regress
the historical full-assembly-path resolution fix.

## Current implementation

### Presentation

EasyWorship 7 uses UI Automation and Win32 focus/input. It locates the main window
by `TFormEZWorshipMain`, selects the Scriptures control, focuses
`TsdScriptureLibrary`, and navigates the mapped Bible list. Unknown position is
anchored using 60 Up messages, then Down to the local index. Subsequent movements
use a session cache and relative delta. Manual changes can desynchronize this cache.

`TsdCustomIncEdit` is a compound reference selector: click the verse side, Shift+Tab
twice, then enter book/chapter/verse through Tab navigation. Read back the reference
and compare before Enter. Preview does not send Enter. Fixed waits remain and must
not be removed without actual UI regression testing. No internal EasyWorship files
are modified. Detailed experimental evidence is preserved in the appendix.

### Audio and recognition

- Windows waveIn captures selected input at 16 kHz, mono, 16-bit PCM in 40ms buffers.
- Raw audio metering, 400ms pre-roll, energy-based gating, background tracking,
  hysteresis and bounded quiet-speech gain. This is not a neural speech/noise classifier.
- Persistent whisper.cpp server, private ephemeral loopback port, /health readiness,
  session filtering and bounded queued work. Stop/restart rejects stale results.
- Fastest = tiny.en; Balanced = base.en; More accurate = small.en. Balanced defaults
  when its model exists. Balanced/More accurate use beam size 3; tiny uses greedy.
- More accurate now accepts the installed small.en Q5_1 model when full-precision
  small.en is absent. Its measured live WER is 30.73% with p95 ready time 5.44 s;
  keep Balanced as default. The compressed model is an evaluation option.
- Runtime setup pins whisper.cpp b5130, verifies runtime SHA-256, and checks model
  minimum sizes. Model integrity checks are not cryptographic. Silero is used by
  the optional neural-filter experiment; it is disabled in the evaluation default.
- The global Bible vocabulary prompt is now empty after real-audio evaluation.
  Long near-total decoding loops are rejected before transcription/detection.
  Neural VAD is available to the replay harness but remains disabled by default;
  tested settings worsened speech accuracy despite passing synthetic noise checks.
- Rolling audio snapshots expose provisional text; these are not native token
  streaming. Snapshot frequency backs off under slow inference, finals outrank
  waiting partials, and older work may be dropped to bound lag.
- Finalization silence is 360/400/440ms by model profile; continuous windows are
  bounded near 6.5 seconds with overlap. A final may reuse a partial only if no new
  voiced audio arrived and that partial already had sufficient trailing silence.
- Windows Speech remains a selectable dictation fallback, not a simultaneous
  constrained-grammar fast lane. A dedicated fast recognizer remains unimplemented.

### Interpretation and action policy

- Standard 66-book aliases, spoken numbers, translation aliases, explicit references,
  contextual verse continuation and brief split-book calls.
- Book-only context lasts six seconds; chapter/verse context lasts 30 seconds.
  Clear/restart resets context. Provisional parsing must not mutate final context.
- KJV/Protestant chapter and verse counts validate references locally. Translation
  omissions and differing numbering are not modeled. Ranges/whole-chapter display
  are not implemented. Quote/paraphrase retrieval is not implemented.
- `Genesis 1-1` and uniquely valid compact forms like `Romans 828` can be interpreted
  as review-only candidates; ambiguous `Luke 115` is rejected.
- Narrow look/tight us/titles suggestions exist at low confidence. They are not a
  solution to poor speech recognition and must not be expanded without evidence.
- Final parser confidence >=0.90 is eligible for optional automatic action. Auto
  Live requires explicit arming, a mapped translation and UI reference verification.
  Identical automatic actions are suppressed for 12 seconds. Context continuation
  is 0.80, inferred number formatting 0.82, phonetic suggestions 0.65. These are
  heuristics, not calibrated recognition probabilities. Provisional candidates
  never automatically prepare or go live. Low-score live actions require confirmation.
- Parser exceptions are caught inside the UI callback. Background logging is
  marshaled to the UI thread. Literal recognized text is retained separately from
  interpreted references. Later unrelated speech retains the last final detection.

### Diagnostics

`scripture-diagnostics.log` includes transcripts, server/model output and timing;
rotates around 2 MB. Application microphone audio is not persisted. The Ready label
measures last voice frame to detection, including waiting/inference/UI time but
excluding EasyWorship automation. It is not the acoustic instant the final word
ended. No systematic real-sermon latency benchmark exists yet.

## Validation and evidence

Run from the application folder with Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Test-ScriptureParser.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Test-AudioPipeline.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Test-SpeechPipeline.ps1
```

Historical v6.3 results: 269 parser checks, 13 audio checks, 9/9 synthetic
Whisper-to-parser examples with base.en. Six were references and three ordinary
sentences. Inference was approximately 1.1 seconds per generated phrase. The report
is retained under `evidence/`; these results do not establish sermon accuracy.
Speech smoke testing requires Windows voice synthesis access, generates its own
WAV/executable/report and starts/stops its own server. It does not use the live
microphone or control EasyWorship. Pure parser/audio checks also avoid those devices.

Evidence categories:
- Proven historically on the church PC: EasyWorship Test/Preview/Live, version-list
  focus/navigation, audio capture/meter and Windows Speech transcription.
- Built and mechanically tested: current v6.3 parser/audio logic and local inference.
- Failed real-world quality target: accurate continuous sermon transcription;
  invented book names reported with identical input to the competitor.
- Proposed, not built: production modular architecture, other adapters, semantic
  quote retrieval, song/media workflows, installer/updater, final speech engine,
  comprehensive performance telemetry and hardware support guarantees.

## Historical milestones

- v1: deterministic controller prototype; build assembly lookup initially failed.
- v2: resolved assembly paths; user confirmed Test, Preview and Live worked.
- v3: direct version-list focus and cached relative version navigation.
- v4: waveIn audio capture and Windows Speech dictation/meter.
- v5: blocking-stream compatibility fix; audio and recognition confirmed working.
- v6: local Whisper, scripture detector and guarded automated actions.
- v6.1: corrected runtime download, checksum, retries and diagnostics; slow speech.
- v6.2: tiny model, shorter segmentation and friendlier labels; performance unverified.
- Parser repair: fixed negative LastIndexOf crash, spoken numbers, numbered books,
  translation precedence, stale context, repeated audio chunk and UTF-8 UI text.
- v6.3: partial windows, quiet capture, metadata validation, isolated speech sessions,
  diagnostics and additional tests. Real sermon comparison subsequently exposed
  significant transcription/hallucination failures despite synthetic success.

## Next engineering work and safeguards

Follow the sequence in PRODUCT_VISION.md. Preserve adapter behavior with tests,
then introduce replaceable speech/understanding/policy/adapter boundaries.
Measure accuracy, false positives, delay, CPU, RAM and download size together.
Do not select larger models solely by reputation or weaken validation to hide misses.

Known limitations include manual version calibration/cache reset, fixed UI sleeps,
DPI-sensitive reference focusing, single-file architecture, handwritten JSON parsing,
limited persisted settings, no production installer or inference crash supervisor,
and no service-length reliability validation. Test focus/manual intervention,
minimized windows, 100/125/150% DPI, dual displays, version-map changes, device loss,
rapid stop/restart, silence/music, queue overload and long services.

The brand document specifies dark/neutral surfaces, restrained Signal Lime,
operator-first behavior and accessible text/state cues. It supplies no actual
vector logo artwork in this workspace. Do not invent/reconstruct the custom mark.
UI modernization and binary renaming are separate implementation work, not done
as part of the documentation cleanup.

## Preserved EasyWorship 7 discovery record

The following historical sections describe the tested EasyWorship 7.4.0.7 / Windows
11 installation. Handles, titles and version indexes are examples, not global
constants. The current implementation summary above takes precedence where versions
differ. Preserve this experimental evidence rather than repeating failed discovery.

# 5. EASYWORSHIP UI AUTOMATION — EXPERIMENTAL DISCOVERY HISTORY

This section records what was actually tested so future work does not repeat it.

## 5.1 Main EasyWorship window

Initial exact-title lookup for `"EasyWorship"` returned nothing.

Enumerating top-level UI Automation windows revealed:

```text
EasyWorship - LIGHT NATION_1 | TFormEZWorshipMain
```

Therefore:

- window title is dynamic because it includes the schedule/project/profile name;
- class name is the stronger locator.

Use:

```text
ClassName = TFormEZWorshipMain
```

Do not hard-code:

```text
EasyWorship - LIGHT NATION_1
```

## 5.2 Scriptures tab

UI Automation found:

```text
ControlType: Pane
Name: Scriptures
ClassName: TsdImageButton
```

Observed one-session AutomationId:

```text
67870
```

The `Scriptures` control exposed no useful `InvokePattern`.

However its `BoundingRectangle` was valid.

The working experiment:

1. locate the control semantically;
2. read current bounding rectangle;
3. calculate center;
4. use Win32 mouse input to click it.

This successfully switched from Songs to Scriptures.

Do not hard-code the test coordinates.

## 5.3 Scripture reference selector

After Scriptures was opened, UI Automation found:

```text
ControlType: Document
ClassName: TsdCustomIncEdit
Name: Genesis 1:1    # changes with current reference
```

Observed one-session AutomationId:

```text
67932
```

Patterns:

```text
TextPattern  -> present
ValuePattern -> absent
```

The control exposed no Book/Chapter/Verse child UIA elements.

Therefore this did **not** work:

```text
ValuePattern.SetValue("John 3:16")
```

The selector is a custom EasyWorship compound control.

## 5.4 First incorrect assumption

Clicking the middle of the visible `Genesis 1:1` control and typing `John 3:16` changed only the verse portion and resulted in something like:

```text
Genesis 1:16
```

This established that the displayed reference is internally composed of separately focusable portions even though UI Automation exposes one element.

## 5.5 Book / Chapter / Verse keyboard behavior

Manual + scripted tests established:

```text
Book --TAB--> Chapter --TAB--> Verse

Verse --SHIFT+TAB--> Chapter --SHIFT+TAB--> Book
```

When a portion is highlighted, typing replaces that portion.

Confirmed examples included:

```text
Genesis -> John
Chapter 1 -> 3
Verse 16 -> 17
```

Then `ENTER` causes the selected scripture to go live.

The preview on the right updates before Enter.

This is important:

> `ENTER` is the final go-live action for the scripture-reference workflow. A separate Go Live button does not need to be automated.

## 5.6 SendKeys focus problem

During PowerShell testing, `.NET SendKeys.SendWait()` often appeared not to work.

Root cause:

```text
SendKeys targets whichever application currently has keyboard focus.
```

Typing/pasting commands into PowerShell meant PowerShell had focus.

The user manually proved the command worked by racing `Alt+Tab` during an intentional delay.

Correct fix:

```text
SetForegroundWindow(EasyWorshipMainHwnd)
```

before sending reference-selector keystrokes.

This was confirmed to bring EasyWorship foreground automatically.

Do not remove foreground management from Book/Chapter/Verse automation.

## 5.7 Current reference-field production strategy

The controller currently:

1. finds `TsdCustomIncEdit`;
2. clicks near its right/verse side;
3. sends `Shift+Tab` twice to force focus back to Book;
4. types Book;
5. Tab;
6. types Chapter;
7. Tab;
8. types Verse;
9. reads the reference control back through UIA;
10. compares expected vs observed;
11. sends Enter only if verification matches.

Current constants include approximately:

```text
ReferenceVerseClickRatio = 0.90
ShiftTabsToBook = 2
```

This is pragmatic and has worked in the controller, but it remains one of the areas to stress-test across DPI/window layouts.

The safety verification before Enter is important. Preserve it.

---

# 6. BIBLE VERSION AUTOMATION — FULL DISCOVERY HISTORY

This consumed significant investigation. Do not restart it from scratch.

## 6.1 Visible EasyWorship behavior

The left-side Scripture library displays only Bible versions that are available/installed in EasyWorship.

On the tested machine the visible sequence included:

```text
AMP
AMPC
ASV
BBE
BSB
ESV
GNT
HAU
HCSB
KJV
LBLA
MSG
...
```

The exact list is installation-specific.

## 6.2 UI Automation limitation

The overall version library is visible as:

```text
ClassName: TsdScriptureLibrary
```

A tested native handle at one point was:

```text
67920
```

Never hard-code the handle; resolve it at runtime.

UI Automation did **not** expose rows like:

```text
KJV
MSG
NIV
```

as named elements.

Tests included:

```text
search UI tree for Name == "MSG"
```

Result:

```text
nothing
```

Also observed:

```text
IsKeyboardFocusable = False
GetSupportedPatterns() -> no useful action pattern
```

## 6.3 MSAA / IAccessible exploration

The older accessibility layer was tested using `AccessibleObjectFromWindow`.

It successfully returned an accessibility object, but only a few generic children.

Attempts to extract useful row names did not reveal the visible Bible entries.

PowerShell COM casting also complicated inspection.

Conclusion:

> MSAA was not a productive route for semantic Bible-row selection on the tested EasyWorship build.

Do not spend time assuming `IAccessible` will trivially return `MSG` unless new evidence appears.

## 6.4 EasyWorship local-file exploration

A read-only probe found global Bible plugin files under:

```text
C:\ProgramData\Softouch\EasyWorship.v7\Plugins\Bibles\
```

Examples included:

```text
KJV.ewb
msg.ewb
esv.ewb
niv.ewb
nlt.ewb
```

This is useful for identifying available resource codes, but it did **not** solve the visible list/order problem.

An important finding:

> Files can exist that are not safely equivalent to the visible current EasyWorship version rows/order.

There was also a known accidental/misplaced `msg.ewb` in profile data during testing.

Do not build runtime selection by simply enumerating all `.ewb` files.

## 6.5 Profile inspection

The active profile appeared under:

```text
C:\Users\Public\Documents\Softouch\EasyWorship\LIGHT NATION_1\
```

The `v6.1` branch appeared current based on recent timestamps.

Read-only inspection of:

```text
Databases
Data
Options\Default.ewo
```

showed settings databases such as:

```text
Live_Settings.db
Foldback_Settings.db
Alerts.db
```

but no confirmed authoritative visible Bible-version order was discovered.

Do not write to these DBs to solve the version problem.

## 6.6 Manual keyboard behavior

Once a Bible-version row actually had focus:

```text
UP / DOWN
```

moved the highlighted row.

Typing version names does **not** type-to-select.

`ENTER` does not activate a Bible row.

Double-clicking a row changes the currently live scripture to that version.

But this key distinction was established:

> Merely highlighting another version immediately changes the Scripture results/preview column.

Therefore when showing a *new* scripture, the workflow does not need to double-click the version.

Correct flow:

```text
highlight desired Bible version
        ↓
set Book / Chapter / Verse
        ↓
Enter
        ↓
new scripture goes live using selected version
```

Double-click is only relevant if one wants to change the translation of the already-live reference immediately.

## 6.7 Direct Win32 message breakthrough

The `TsdScriptureLibrary` control has a real native window handle.

Sending:

```text
WM_KEYDOWN / WM_KEYUP
VK_DOWN
```

directly to that handle moved the Bible selection down **exactly one row**.

This was confirmed experimentally.

This means Bible-version movement does not need physical mouse positioning or normal SendKeys focus.

## 6.8 HOME does not work

Manual Home testing did not provide a useful top jump.

Directly sending:

```text
VK_HOME
```

to `TsdScriptureLibrary` also did not produce a useful top anchor.

Do not use Home.

## 6.9 Reliable top anchoring

The working normalization method is:

```text
send more VK_UP messages than the list could need
```

Example tested:

```text
VK_UP × 60
```

Result:

```text
selection lands at first installed Bible
extra UP messages leave it there
```

This gives a deterministic top anchor even if the current version is unknown.

## 6.10 Index jump confirmed

On the tested church PC:

```text
0  AMP
1  AMPC
2  ASV
3  BBE
4  BSB
5  ESV
6  GNT
7  HAU
8  HCSB
9  KJV
10 LBLA
11 MSG
```

The following was tested:

```text
UP × 60
DOWN × 11
```

Result:

```text
MSG highlighted exactly
```

So runtime movement by index is confirmed.

## 6.11 Direct focus of version list

A major remaining problem was that version movement only worked if the list was highlighted/focused.

A test used Win32:

```text
SetForegroundWindow
GetWindowThreadProcessId
GetCurrentThreadId
AttachThreadInput
SetFocus(TsdScriptureLibrary hwnd)
```

Then sent one `VK_DOWN`.

The user explicitly confirmed:

> The version row became highlighted and moved down exactly one version even though it had not been highlighted before.

This is now the correct version-focus strategy.

## 6.12 Fast session state tracking

The first implementation always did:

```text
UP × 60
then DOWN × targetIndex
```

This was reliable but unnecessarily slow for versions near the end.

The controller keeps process-scoped selection state in BibleVersionPosition.
It initially assumes KJV at the configured index; successful switches update this
state. Future transitions use only the delta. An unchanged version returns before
even focusing the list. Stopping listening preserves the selected version.

Example:

```text
tracked KJV = 9
requested MSG = 11

DOWN × 2
```

instead of:

```text
UP × 60
DOWN × 11
```

If requested index is lower, send `VK_UP` by the delta.

If same index, send nothing.

### Manual-operator caveat

If a human manually changes the Bible version in EasyWorship, the controller's cache can be stale.

The UI therefore includes:

```text
Reset version tracking
```

which clears the cached state.

The next automated change then safely re-anchors from the top once.

A future improvement is to automatically detect external/manual version changes if a reliable signal can be found.

---

# 7. PER-CHURCH VERSION MAP

Runtime index movement is solved.

Automatic discovery of the visible Bible version → index mapping is **not** fully solved.

Current local config format:

```text
AMP=0
AMPC=1
ASV=2
BBE=3
BSB=4
ESV=5
GNT=6
HAU=7
HCSB=8
KJV=9
LBLA=10
MSG=11
```

Stored in:

```text
version-map.txt
```

These values are only confirmed for the tested `LIGHT NATION_1` machine.

Do not treat `MSG=11` as globally true.

Potential future setup-time mapping strategies:

1. guided manual calibration;
2. OCR-assisted one-time calibration + human confirmation;
3. deeper read-only discovery if EasyWorship exposes authoritative ordering somewhere;
4. a setup wizard that walks the rows from top to bottom.

Important distinction:

> Calibration can be slow because it happens at setup. Runtime selection must remain fast.

Do not introduce OCR during every live scripture call.

---

