# Gazely engineering handoff

Current baseline: v6.3 prototype. Consolidated 30 September 2026.

[Product vision](PRODUCT_VISION.md) governs product scope and constraints.
[Brand guidelines](Gazely_Brand_Guidelines.md) governs identity. This document
records implementation and evidence; proposed architecture is not a claim of
shipped functionality.

## Immediate engineering reality

### 15 October selected passage and Mini follow-up

A complete spoken reference now replaces an operator-selected quotation in the
main Selected Passage panel. The earlier quote remains beneath as a selectable
alternative. Incomplete later speech does not erase the complete verse, and
manual reference edits retain their separate operator hold. Mini compact and
expanded views now show the workspace's KJV scripture preview, with an explicit
output-version label when the requested version differs. The UI suite exercises
the reported Luke 1:17 wording through the actual final-transcript path and
checks Mini rendering and fake-adapter safety.
The parser also now prefers a complete book/chapter/verse call over a later
book-like word in the same sentence ("Josh" after "Luke 1:17"); the exact
reported utterance passes its parser fixture.

Bible navigation now uses the fresh highlighted library code, when readable and
present in the operator-confirmed map, as the origin for a direct index delta.
It leaves the list untouched when the code is already selected. An unreadable
or unmapped selection still uses the top re-anchor for manual navigation;
automatic output pauses when selection cannot be freshly verified. The 21
navigation checks and 24-row local catalogue check pass without commanding
EasyWorship. The parser suite, full UI/Mini suite with the exact Luke utterance,
passage policy, and 35 adapter lifecycle checks pass. The rebuilt candidate
SHA256 is `EC93B2D2177C290FF0E5CBDBA39298DF8AA9FEAAC353B137EDFD15EFB31F973D`.
Only `Gazely.mvp-candidate.exe` was rebuilt for this follow-up; the user asked
to defer a new installer. Direct relative version movement still needs a live
EasyWorship readback test on an exposed window; this run used state tests and
no real presentation command.

### 15 October onboarding and local Bible discovery update

The candidate now proposes the Bible list from read-only SQLite `header.abbrev_name`
metadata in `C:\ProgramData\Softouch\EasyWorship.v7\Plugins\Bibles\*.ewb` using
Windows' installed `winsqlite3.dll`. It reads no Bible text and writes nothing to
EasyWorship. On LIGHT NATION_1, 24 readable modern headers sort to exactly the 24
rows in the overlapping screenshots; 12 older-format files are excluded. The
displayed abbreviation is **PIG**, not the earlier handoff's PTG typo; the local
map and readback allowlist were corrected. This exact match is evidence for this
PC, not a proven rule that every readable plugin is visible on every profile.
The proposed list still requires operator comparison with EasyWorship before
saving; missing/incomplete headers fall back to manual entry. Automatic output
still needs fresh selected-library verification at the action edge.

First run now opens a short guide, with a permanent How to tab covering Bible
review, audio, Live workspace and Mini. The Bible dialog prepopulates from the
local headers; Enter inserts a line, and a successful save returns to Live
workspace. Mini's operator-monitor dialog explains its controls and display
safety. Audio-device enumeration now refreshes Start Listening's enabled state
after its asynchronous load. The candidate was rebuilt and packaged as the
self-contained offline `releases/Gazely-Setup-20261015-r3.exe` (997,714,194
bytes; SHA256 `A803435C1C28DC5B77A37034F18540111327F0DB95E2DB5FF82CFAF74A667998`).
Both bundled speech packages verified offline. A quiet install to an isolated
workspace folder exited 0, matched the archive receipt and candidate EXE, and
included 363 manifest files with no prefilled Bible map. The isolated EXE
launched and created an empty local map and diagnostics; no presentation action
was issued. New interface/first-run/Mini screenshots and tests passed, as did
37 adapter checks, 19 navigation checks, passage policy and speech lifecycle.
The optional chapter-pronunciation recovery could not initialize the installed
Windows voice in this isolated launch (NullReferenceException); it fails closed
and remains review-only. Target 8 GB validation and live EasyWorship selection
readback remain unverified. Earlier r2 installers and production/rollback builds
remain intact; nothing was downloaded or published.

### Fresh-chat continuation — 15 October 2026

Offline installer update: `releases/Gazely-Setup-20261015-r2.exe` is a
self-contained 997,710,410-byte per-user Windows setup EXE, built only from the
candidate and installed local assets. SHA256:
`89AC374BC1D2F2DB3B63B731FA5937B8B9B1B699D4E2D09AC455E77BF379DB3C`.
The ZIP payload excludes the local Bible map, preferences, diagnostics, caches,
staging files and old executables. Its 363 file hashes passed after extraction.
The setup EXE was run in quiet mode into an isolated workspace install path; both
speech packages verified offline there, and the installed Gazely EXE launched
with 31,102 local verses ready and zero assumed Bible indexes. This is an
installer smoke test on the development PC, not target-machine validation or a
hosted download. No files were downloaded. The public installer is unsigned;
Windows may show a publisher warning. Candidate and production/rollback builds
were not staged or replaced. Latest candidate SHA256:
`59C8295971F327B912C552F84296B32F271F334A58CFAF86CB848ACA7A16A555`.

Fast-release update: the user waived the proposed two-hour endurance run for
the first public build and will test the product personally. Two 30-minute paced
replays passed; the waiver is not a new pass. The candidate now gives each new
installation an empty `version-map.txt` and a guided Bible versions setup based
on the abbreviations actually shown in that user's EasyWorship Scriptures list.
The existing LIGHT NATION_1 map is preserved locally. Setup validates unique
codes and contiguous positions, then asks for order confirmation; it writes only
Gazely's local map. The earlier manual-only discovery status is superseded by
the local-header proposal above, and
automatic output continues to pause without fresh selected-library readback.
The final page render, 34 adapter checks, full interface suite and isolated clean
first-run map check pass; target 8 GB hardware
and real EasyWorship selection readback remain unverified and must be disclosed
for the initial release. React and signup/Supabase remain deferred.

15 October continuation: the shared screenshot confirms MSG in both the highlighted
Scriptures library row and the results table's Trans… column, while the live slide
still says KJV. A second, overlapping screenshot reaches YOR followed by
“More Available…” and Collections. Together they show 24 local installed rows,
ordered by displayed abbreviation: AMP, AMPC, ASV, BBE, BSB, ESV, GNT, HAU,
HCSB, KJV, LBLA, MSG, NASB, NBLH, NIVA, NKJV, NLT, PIG, RST, RVA, TLV, TPT,
WB, YOR. The ignored local `version-map.txt` now records contiguous indexes 0–23
for LIGHT NATION_1. This is screenshot evidence for this profile, not a global map.
The running EasyWorship process exposes no top-level window to this automation
session; dynamic catalogue enumeration and table freshness remain unverified.
Automatic translation output remains paused when the selected library translation
is unreadable. The candidate recognizes all 24 codes and requires fresh selection
readback at the action edge even when version navigation is disabled.

The failed soak's time jump has a concrete cause: Windows Power-Troubleshooter
recorded battery sleep on 14 October and resume on 15 October, matching the
~69.9-million-ms gap in events.tsv. The original run has no completion summary.
A new 30-minute replay was started in `benchmarks/runs/mvp-soak-rerun-20261015`;
it completed with exit code 0, 1,800 audio seconds, all 28,800,000 samples
covered, 447 finals, 1,796 partials, 0.4-second maximum sampled backlog,
270-ms EOF flush, 1,789,300,736-byte peak summed RSS, 282 retained candidates
and zero adapter calls. Largest logged event gap was under 446 ms. This passes
the paced continuity and no-output check on this PC, not the target 8 GB check.
The replay harness now requests that Windows keep the system awake and rejects
wall-clock gaps above 15 seconds for future paced runs.

The 282 retained candidates included 240 review-only paraphrase rows, too many
for a 30-minute operator session. A new semantic evidence gate requires at least
seven source words and reranker score >=4.5, with at most three qualified
alternatives per phrase. It leaves lexical and explicit paths unchanged and grants
no automatic permission. Offline rescoring of the held-out text fixture retained
14/15 paraphrase hits and 8/8 negative abstentions; the reported-sermon text
fixture retained 4/6 hits and improved from 1/2 to 2/2 negative abstentions.
Requerying 140 unique replay paraphrase evidence strings reduced 305 raw worker
records to 36 qualified records. The second paced replay of the gated candidate
at `benchmarks/runs/mvp-soak-gated-20261015` completed with exit code 0:
1,800 audio seconds, all 28,800,000 samples, 447 finals, 1,796 partials,
0.6-second maximum sampled backlog, 265-ms EOF flush, 1,934,802,944-byte
peak summed RSS and zero adapter calls. Largest logged event gap was under
588 ms. Retained candidates fell from 282 to 74, with paraphrase rows from
240 to 32; explicit (10), quotation (15), contextual (4) and unresolved (13)
counts stayed identical. This is a relevance improvement on one replay, not a
labeled real-sermon precision/recall claim. Summed RSS is above the initial
1.5-GiB recognition-plus-retrieval budget and is not whole-PC RAM usage.
The candidate remains unstaged while the short initial-release package and
first-run checks are completed. Live EasyWorship readback, held-out audio quality
and target 8 GB coexistence remain unverified limits; the fast-release decision
above governs the first public build.
The earlier candidate hash is superseded by the installer update above.
Production `Gazely.exe` and `Gazely.previous.exe` retain their prior hashes;
`git diff --check` and the final offline build pass. No replay/native speech
processes remain running. Local `version-map.txt` is intentionally ignored by
Git and must be recalibrated for another EasyWorship profile or installation.

Candidate build, 26 adapter checks, 19 navigation checks, passage policy, speech
lifecycle, 333 parser cases, 19 quotation fixtures and the full Mini/interface
suite passed on 15 October. Both installed speech packages passed offline hash
verification. An isolated 7-byte package fixture confirmed cancellation staging
cleanup, offline resume, corruption detection and preservation of the prior
package on repair. No EasyWorship file or real presentation command was used.
The isolated fixture also rejected an invalid offline cache during repair,
left no staging directory and retained the previous package copy.
The installed-model `Test-SemanticSearch.ps1 -HeldOut` fixture hit 14/15
author-written paraphrases and abstained on 8/8 negatives (startup 1,301 ms,
p95 query 421 ms, worker peak 449 MiB). The miss was 1 Corinthians 3:6; this
small text fixture is not real-sermon quote recall or a calibration for automatic
presentation. Semantic candidates remain review-only.
The verified installed speech packages occupy 164,314,589 bytes (Whisper) and
737,021,328 bytes (Nemotron), 901,335,917 bytes together before search assets;
this exceeds the original 750 MB combined target. Both installed license files
are present. Target 8 GB hardware and end-to-end offline launch remain release
checks, and the candidate is not staged.
Adding local search runtime (85,935,762 bytes), search models (153,228,064),
corpus (13,421,733) and candidate EXE (310,784) gives approximately
1,154,232,260 bytes for these measured components, excluding settings and any
additional installer overhead. This footprint needs an explicit release decision.

Read EXECUTION_PLAN.md's newest section first: it adds verified screenshot evidence
for abbreviation sorting and the library results table's Trans… column. These are
next investigation targets, not completed automatic discovery. No downloads are
authorized. Preserve all uncommitted changes; do not reset or overwrite them.

The second soak is **not a pass**: on 15 October no summary.txt exists in
`benchmarks/runs/mvp-soak-packaged-20261014`, and no NativeReplay/nemo-speech process
was present. Its tail has approximately 71,052 seconds wall time but only 18,144,000
audio samples (1,134 seconds at 16 kHz). This is not an uninterrupted 30-minute
validation; investigate the interruption/time gap and rerun locally when suitable.
Do not present the earlier “in progress” note below as current status.

Working sources live in `EasyWorshipController_MVP_v6_2_CODEX_HANDOFF`:
EasyWorshipController.cs (speech/state/retrieval/automation), GazelyInterface.cs
(workspace/Mini), PresentationAdapters.cs (adapter policy), speech-runtime/
nemotron_worker.py, Install-SpeechPackage.ps1 and speech-runtime/packages.json.
Only Gazely.mvp-candidate.exe was rebuilt; production/pending/rollback binaries
were not replaced. Runtime packages already exist under speech-packages; do not
download dependencies again. Speech-package checks must use `-Offline`.

Offline regression entry points in that app directory: Test-PassagePolicy.ps1,
Test-SpeechLifecycle.ps1, Test-PresentationAdapters.ps1, Test-VersionNavigation.ps1,
Test-ScriptureParser.ps1, Test-QuotationIndex.ps1 and Test-Interface.ps1 (requires a
new -OutputDirectory). Build with Build-EasyWorshipController.ps1 -OutputFileName
Gazely.mvp-candidate.exe. The production replay harness is
`benchmarks/Test-NativeLiveReplay.ps1`; use one ASR evaluation at a time.

Remaining engineering gaps: validate quotation thresholds on held-out/negative
fixtures; cap qualified alternatives per quotation without losing separate quotes;
improve generic-match suppression with evidence; finish version discovery/readback;
exercise installer cancellation/resume/corruption cleanup without network; complete
package licence/offline startup checks and sustained replay. Review UI-thread
shutdown/error cleanup and stale callbacks. Verify real 8 GB hardware separately.
Latest UI suite `mvp-interface-20261014-mini-b` passed, including Mini recovery.
Signup/Supabase and React remain outside this implementation phase.

### Stable MVP candidate — 14 October 2026 (not staged)

Current work is in `Gazely.mvp-candidate.exe`; the staged build described below
remains unchanged. No further downloads are authorized as of 14 October. Use
installed assets and offline checks only; do not invoke an installer without
`-Offline` during this work.

The candidate introduces evidence-based passage protection and quotation
promotion, separate preview/live eligibility, editable pending speech settings
with asynchronous restart, Whisper/Nemotron engine IDs, shared Mini alternatives,
and migration of Present controls into workspace/Integrations. Standalone runtime
packages have been assembled locally. Packaging, UI polish and release validation
remain incomplete; these changes are not a production-readiness claim.

EasyWorship library observation uses bounded read-only UIA/MSAA probes. The actual
installed application's selection remains unreadable in the probe, so automatic
output is paused for unverified translation state. Calibrated manual presentation
remains available. A last observation is labelled with its time and rechecked
before output; process/catalogue changes and explicit resets reject stale probes.
Preview and Live now use different automatic-action deduplication keys, allowing
newly corroborated evidence to progress from preview to live without the preview's
12-second suppression window blocking it.

Latest offline checks: candidate build, passage policy, speech lifecycle, 26 adapter
checks (including translation verification) and 19 legacy navigation checks passed.
An earlier 30-minute paced replay disconnected at approximately 651 seconds; the
cause is unresolved. A subsequent 80-second replay around that point using the
standalone Nemotron runtime passed: maximum backlog 0.9 seconds, combined peak
RSS 1,659,564,032 bytes, final flush approximately 826 ms, no presentation actions.
A new 30-minute run is in progress at `benchmarks/runs/mvp-soak-packaged-20261014`;
do not count it as passing until its summary and successful completion exist.
Held-out automatic-output checks, final UI checks and an actual 8 GB hardware
validation are still release gates. Preserve the existing rollback and pending
build until the candidate passes the required checks.

Further offline UI validation: `mvp-interface-20261014-mini-b` passed all six
pages, passage/translation regressions, Mini recovery, monitor confinement,
persistence and shared presentation actions. Compact/Expanded screenshots were
inspected. Mini now includes the existing PNG symbol, larger minimum dimensions
to fit its controls, and explicit startup/stopping/failure status. The candidate
rebuilt successfully. The second paced replay has passed the earlier disconnect
point but still requires its full 30-minute completion result.

### Active-passage translations — 12 October 2026

Staged after the Nemotron integration. ScriptureDetector now retains both the
translation and its explicit-request flag across verse continuations, navigation,
active-reference seeding, verified display context and preview snapshot/restore.
New named-book full references reset to default KJV when no translation is given.
Chapter-only drafts retain an explicitly requested version until their verse arrives.
Standalone directives ("use the Message", "give me that in NLT", code-only "NIV")
change a complete selected passage without inventing numbers; they remain
review-required. Ordinary mentions, negated commands and unanchored requests abstain.
Operator-held selections retain their existing protection from speech changes.

SP now distinguishes local KJV preview text from a requested non-KJV output.
The existing mapped-version check still blocks missing mappings. No EasyWorship
files were changed, and no adapter interfaces or ASR settings changed.

Validation: parser and quotation regressions; full `ui-translation-context-2`
suite including actual OnFinalTranscript → SP → shared fake adapter sequence
John 3:16 NIV → 3:17 NIV → 3:17 MSG → 3:18 MSG → Romans 8:1 KJV;
split chapter/version → verse; overlay recovery/persistence and prior SP tests.
No real presentation commands. First UI run needed its expected fake-command
counter updated to include the five new intentional test presentations.

Latest staged `Gazely.pending.exe` SHA256:
`82D30869EDED13BDE76D33B1689C752420ED8399A9F2CA842C07EEB00BEA07EF`.
Close Gazely and launch `RUN_GAZELY.bat`. Nemotron remains experimental/manual-only.
The 30-minute endurance and real EasyWorship coexistence test remains outstanding.

### Nemotron live integration — 12 October 2026

`StreamingSpeechEngine` now supports **Nemotron English (experimental)** alongside
the existing Moonshine choices. Whisper stays the default; native streaming
cannot trigger automatic Prepare/Live. Manual actions reuse the same adapter,
including from Mini overlay. No presentation-adapter interface changed.

- One Python worker and one native NeMo-Speech v0.2.0 CPU server per listening
  session; binary PCM over authenticated loopback WebSocket, persistent decoder
  caches, four CPU threads, right context 6 / 560 ms, 700 ms endpointing. No
  hosted ASR, per-packet WAVs, model reloads, vocabulary prompts or recordings.
- C# bounds its PCM queue; the worker also bounds unacknowledged transport audio.
  Ordered ping/pong acknowledgements advance during silence. Transcript events
  are not a valid progress signal: silence can emit none. Logs show received and
  acknowledged samples, queue occupancy and acknowledgement roundtrip. These
  timings are not model-only compute time or spoken-reference display latency.
- EOF waits for native commit confirmation. Windows job ownership removes the
  native child if the worker dies. Empty final revisions clear pending live text.
- Contextual ranges split across final transcript lines now select their first
  verse. Punctuation-free navigation after discourse cues is review-required;
  it does not gain automatic-presentation permission.

Evidence (local, ignored `benchmarks/runs`):

- `nemotron-bridge-websocket-4.tsv`: two fresh paced 102-second sessions, 38 final
  and 158 partial events, no duplicate final IDs/errors, EOF flushed.
- `nemotron/live-genesis-2`: 240-second production callback replay with lexical
  and semantic retrieval; 67 finals, 252 partials, maximum sampled acknowledged
  audio backlog 0.3 s, EOF flush 254 ms, all 3,840,000 samples represented, zero
  adapter calls. WER 65/449 = **14.48%**, matching the accelerated file benchmark.
  SP followed Genesis → Genesis 49 → 49:3 → 49:4. Matthew 6:33 appeared in Detections.
- `test_nemotron_worker.py`: ten seconds of paced silence acknowledged completely
  without invented words; EOF completed; abruptly killing the worker removed its
  native child. Existing file noise/hum checks are not long live-noise certification.
- Parser, quotation, 31 audio-pipeline checks and `ui-nemotron-final` full interface
  suite passed, including overlay menu recovery, preferences, shared actions and
  native-engine manual-only policy. No real microphone or presentation commands.

Measurement corrections: `live-genesis-1` finished transcription but its metrics
sampler threw after native exit; `live-genesis-2` passed. Initial RSS totals in
`live-calls-1` and `live-genesis-2` undercount cached Process counters and omit the
venv's child interpreter; those memory totals are invalid for acceptance. The
sampler now refreshes counters and includes the actual interpreter PID.

Corrected rerun `nemotron/live-calls-2`: 102 seconds, 19 finals / 79 partials,
maximum sampled backlog 0.3 s, combined peak working sets **1,680,957,440 bytes
(1.68 GB / 1.57 GiB)**, EOF flush 275 ms, all 1,632,000 audio samples represented,
24 retained candidates, zero adapter calls. This includes the replay UI process,
Python launcher/interpreter, native ASR and semantic search worker; working-set
sums can double-count shared pages and are not a whole-PC memory measurement.

Staged `Gazely.pending.exe` SHA256:
`A366326E20F1A3CD842386B1F8A888A1614B23BAAA3065FA6C6B490B4A2E3657`.
Close Gazely, launch `RUN_GAZELY.bat`, then select **Speech setup → Nemotron
English (experimental)**. This supersedes the October 11 overlay-only pending
build and retains its fixes. The updater preserves the prior executable.

The official Q8 English model is 699,872,960 bytes (~700 MB), above the original
500 MB speech budget and explicitly downloaded for this later evaluation. It is
not Nemotron 3.5. Parakeet is downloaded/load-checked but not selectable yet; its
current ONNX export lacks streaming cache inputs. No production defaults changed.
Thirty-minute endurance, held-out speaker accuracy, physical microphone/device
latency and total memory alongside presentation software on an 8 GB PC remain
release gates. These short replays do not certify those targets.

### Mini Overlay implemented and staged — 11 October 2026

`MainForm.MiniOverlay` in `GazelyInterface.cs` is a borderless, resizable,
topmost tool window using WS_EX_NOACTIVATE and WM_MOUSEACTIVATE/MA_NOACTIVATE.
Open it through **Mini overlay** in the main navigation. It hides the full
workspace, which remains alive as the sole owner of audio, interpretation,
retrieval and presentation. The arrow and tray icon return to full settings.

- Collapsed: status, meter and listening control. Compact: selected passage,
  default/explicit version, matching score, Preview and Show Live. Expanded adds
  audio source, inline transcript, shared automation toggles and six-per-page
  detections (all active suggestions remain reachable). Select reviews a
  snapshot; the adjacent play icon presents through the existing adapter.
- Detection cards refresh only when their displayed data changes, and defer
  rebuilding while the cursor is over the list. This avoids moving an action
  under an operator's pointer. Page 1 is newest; refresh returns to page 1.
- First use requires operator-monitor selection. Bounds clamp to that monitor's
  working area. Missing monitor or an anchored target on another monitor hides
  the overlay; there is no automatic fallback onto a potential projector.
  Mirrored displays/external operator-screen capture cannot be excluded by this
  window style; deployment must use a distinct extended presentation output.
- Optional corner anchoring reads window geometry only. It follows a selected
  EasyWorship/ProPresenter window and hides on minimize/close; tray recovery
  remains available. ProPresenter anchoring is not a new output adapter.
- Existing SubmitPresentation hides the overlay before adapter execution, then
  the timer restores it after completion, preventing interception of automation
  clicks. No EasyWorship files, configuration or database are modified.
- Namespaced `overlay.*` preferences preserve monitor, position, per-mode size,
  mode, anchor and enabled state. Full-workspace saves now merge existing keys.
  Listening still does not start automatically on application launch.
- Overlay options now live until overlay shutdown. Disposing ContextMenuStrip
  synchronously in Closed invalidated WinForms' pending click dispatch and could
  strand the operator in Mini mode. Returning now restores the prior maximized
  workspace state; repeated menu and arrow return paths are regression-tested.
- WM_WINDOWPOSCHANGING clamps drag/resize bounds before Windows applies them,
  in addition to post-placement constraints.

Validation: offline build; full interface regression suite including synthetic
anchor minimize/restore, disconnected-monitor hiding, out-of-bounds placement,
non-activation style bits, settings preservation/reload, and actual shared
Present through a fake adapter. Screenshots inspected for compact/expanded
layout. No real microphone or presentation command used. Physical multi-monitor,
mixed-DPI and real EasyWorship placement still require operator testing.
Evidence: `benchmarks/runs/ui-overlay-final`, `benchmarks/runs/ui-overlay-boundary`
and `benchmarks/runs/ui-overlay-menu-fix-3` (ignored).
Staged `Gazely.pending.exe` SHA256:
`952399FBAD4C1B9C23E1E573EECB41B1AFDFC36DE8B2B50A78DCAAFDBD470014`.
Activate by closing Gazely and launching `RUN_GAZELY.bat`; previous executable is
retained by the existing updater. No speech model default was changed.

Nemotron evaluation continued separately using `benchmarks/benchmark_nemotron.py`:
240-second Genesis PCM, right context 1 (160 ms), four CPU threads, native v0.2.0.
WER 66/449 = 14.70% against the existing user-corrected reference (same raw-word
normalization as the historical Moonshine run). Elapsed 369.52 s including model
startup, 0.649x audio/wall throughput, sampled worker peak RSS 997,294,080 bytes.
This configuration fails the live-throughput gate despite improved wording.
No retrieval contention in this run; no claim of 8 GB whole-app certification,
reference-end latency or held-out accuracy. Evidence:
`benchmarks/runs/nemotron/genesis-c1-t4`.

The sequential right-context 6 (560 ms) run on the identical 240-second PCM took
62.59 s including startup (3.834x throughput), with 65/449 word errors = 14.48%
and 1,000,996,864-byte sampled worker peak RSS. Evidence:
`benchmarks/runs/nemotron/genesis-c6-t4`. Both runs used four CPU threads and the
same pinned model/runtime; these are single measurements under desktop load,
not a controlled hardware certification or a calibrated latency guarantee.
560 ms is the next integration/evaluation candidate; retain production defaults
until retrieval contention, sustained real-time feeding, final flush and
reference-end latency are verified. Parakeet remains download/load-checked, not
yet comparatively scored. No transcription engine promotion was included in
the overlay build.
The fixed 10-second silence, white-noise and hum fixtures each produced empty
transcripts at right-context 6, with successful EOF completion and no runtime
error. This small synthetic suite does not establish real venue-noise accuracy.

### Streaming candidate downloads authorized — 11 October 2026

User authorized all needed candidate models, lifting the previous download hold.
Scope: the discussed Nemotron English and Parakeet Unified candidates, using CPU
quantizations; no unrelated LLM, training checkpoint or duplicate FP32 weights.
`benchmarks/Get-StreamingCandidates.ps1` downloads pinned revisions into ignored
`benchmarks/tools/streaming-candidates`, resumes partial downloads and verifies
SHA256 before promotion. `benchmarks/streaming-candidates-manifest.json` records
actual byte sizes, checksums and source URLs.

- Nemotron English Q8_0: 699,872,960 bytes, official NVIDIA model.
- Parakeet Unified INT8 encoder/decoder/tokenizer: 663,286,254 bytes, community
  ONNX export (633 MiB, not 633 decimal MB).
- NVIDIA NeMo-Speech.cpp v0.2.0 Windows x64 CPU runtime: 5,528,418-byte archive,
  checksum verified and unpacked locally; no PATH/global installation changes.
- Existing Whisper, Moonshine and retrieval assets remain available.

Installation smoke checks: Nemotron loaded its explicit local GGUF and completed
102 seconds of existing `calls.wav` with `--stream`, CPU, four threads and 160 ms
steps. Output is `benchmarks/tools/streaming-candidates/nemotron-smoke.json`.
Genesis 49:3-4 and the Leviticus six/verse thirteen/eleven/twelve wording appeared;
Malachi remained imperfect. This was not a timed controlled accuracy comparison.
One sampled process working set was about 988 MB; it is not total Gazely memory
or a measured maximum over the whole session. Output confidence values of 1 must
not be treated as calibrated certainty. No presentation commands were issued.

Parakeet encoder and decoder loaded with CPUExecutionProvider; tokenizer has 1024
pieces. Encoder inputs are features and lengths (no cache inputs), so the export
must not be advertised as a ready cache-aware streaming engine. Its isolated
Python dependencies are separate from the working Moonshine environment.
CPU PyTorch/torchaudio imports and 128-bin mel feature computation passed;
`pip check` passed. Installed versions are recorded in
`benchmarks/parakeet-requirements.lock.txt` (CPU PyTorch wheels use the official
`https://download.pytorch.org/whl/cpu` index). Conversion/training NeMo is not
required for these already-exported ONNX files and was not installed.

Neither candidate is wired into Gazely's engine selector yet. Next: sequential
controlled replay with retrieval, reference/word accuracy and sustained latency
measurement, then integrate only the qualifying candidate. Existing production
defaults and restart-update artifacts are unchanged by these downloads.

### Inline transcript and same-book quotation retention — 10 October 2026

Live partials now revise the current transcript line in place, retaining its first
timestamp; final text replaces that line. The separate live-text label is hidden
in the live workspace. Older finals preserve a newer pending line, Clear resets
both buffers, and Stop discards unfinished partial text after engine drain.
Updates replace only the changed text suffix, preserving a user's selection
where possible. This changes presentation, not ASR endpoint timing or accuracy.

A displayed complete quotation previously could exist outside ActiveReferenceState.
Book-only emphasis in that same book now retains the displayed numbers as an
unconfirmed, review-required reference. Different books and explicit chapter
changes do not inherit these numbers. Existing operator overrides remain intact.
The reported “efficiency of the form” transcription has not been converted into
a global Ephesians alias; its numbers cannot safely be reconstructed from text.

Validation: offline build; 330 parser cases and supplementary checks; new selected
quotation-retention and transcript revision/order/reset checks; full existing
interface regression suite passed with a fake presentation adapter and no mic.
Rendered split-reference screenshot inspected. Evidence is under ignored
`benchmarks/runs/ui-inline-20261010`. Candidate staged as `Gazely.pending.exe`;
close Gazely and launch `RUN_GAZELY.bat` to activate with the existing rollback.
No model downloads, speech decoder changes or real presentation commands.

### Streaming alternatives: source review only — 9 October 2026

User instruction: **do not download any new model yet**. No new model or runtime
was installed, and no application engine/default was changed for this review.

Moonshine's operator test reached the 20-second backlog stop. Existing replays
also fell behind without retrieval. This establishes insufficient sustained
throughput in the tested configuration, not the exact underlying cause. Thread
settings, native decoding cost, thermal/load effects and scheduling remain to be
isolated. Increasing the queue limit would merely delay failure. Its aggregate
WER result must not be presented as proof it beats the user's best Whisper run.

Candidate assessment:

| Candidate | Verified route | Size and remaining limits |
|---|---|---|
| Nemotron Speech Streaming EN 0.6B | Official NVIDIA NeMo-Speech.cpp supports cache-aware streaming and a native C ABI. Windows x86-64 CPU v0.2.0 release archive exists (5.27 MB). | Official Q8_0 GGUF is listed at approximately 700 MB, exceeding the 500 MB combined speech-weight budget by itself. Gazely peak RAM and sermon accuracy remain unmeasured. |
| Parakeet Unified EN 0.6B | NVIDIA's streaming implementation uses buffered inference; community INT8 ONNX conversion documents offline inference. | Community report of 633 MB and roughly 1 GB RAM is not a validated Windows streaming configuration. Unified must not be confused with Parakeet TDT v3 supported by NeMo-Speech.cpp. |

Nemotron is the preferred **next evaluation candidate**, conditional on approval
of the larger download/budget. Its cached encoder state is a better fit for
continuous CPU streaming than repeatedly recomputing a long context. This is an
architectural rationale, not proof of better accuracy on Gazely's recordings.

NVIDIA's CPU benchmark reports 27.0 ms average computation per 160 ms chunk,
5.6x throughput and 3.00% WER. This used an i7-11700K with eight threads on Ubuntu,
on a 100-utterance LibriSpeech test-clean subset (13.3 minutes). The host had
128 GB RAM; that is not a measured memory requirement. These figures neither
certify an ordinary 8 GB Windows laptop nor predict Nigerian sermon accuracy.
Compute time is not microphone-to-selected-passage latency.

Integration design, not implemented:

1. Pin a tested runtime revision and explicit local model path. Do not invoke
   default CLI model resolution: it can download a different default model.
2. Use an isolated native worker initially, keeping crashes outside WinForms.
   Convert existing PCM16 mono 16 kHz to float32, then use the C ABI's streaming
   create/push/next/finish calls. Drain the final result before closing a stream.
3. Preserve session and utterance identities. Update the current transcript
   paragraph in place for interim text and freeze it on final; do not append
   partial duplicates. This requested transcript UX is still pending.
4. Keep interpretation and presentation adapters unchanged. Provisional text
   must not silently commit uncertain chapter numbers or trigger live output.
5. Replay the same sermon excerpts with retrieval, then 30 minutes continuously;
   measure exact references, WER, endpoint delay, backlog, CPU and combined peak
   memory. Compare the user's actual Whisper configuration, not just one control.
6. Confirm the chosen release exposes the inspected API; main-branch headers may
   be newer than the binary. Validate licensing and packaging. Windows text
   normalization support is limited, so retain Gazely's number interpretation.
   Stage only after local results pass; preserve the existing rollback build.

Sources inspected (metadata/documentation only):
- [Native runtime](https://github.com/NVIDIA/NeMo-Speech.cpp)
- [Windows CPU release assets](https://github.com/NVIDIA/NeMo-Speech.cpp/releases/expanded_assets/v0.2.0)
- [Official model files](https://huggingface.co/nvidia/nemotron-speech-streaming-en-0.6b/tree/main)
- [CPU benchmark and methodology](https://github.com/NVIDIA/NeMo-Speech.cpp/blob/main/BENCHMARK.md)
- [Streaming C ABI](https://github.com/NVIDIA/NeMo-Speech.cpp/blob/main/include/nemo_speech/asr.h)
- [Installation and Windows limitations](https://github.com/NVIDIA/NeMo-Speech.cpp/blob/main/docs/install.md)
- [Parakeet Unified model](https://huggingface.co/nvidia/parakeet-unified-en-0.6b)
- [Community ONNX conversion](https://huggingface.co/eschmidbauer/parakeet-unified-en-0.6b-onnx)

### Moonshine operator-test build staged — 8 October 2026

Latest user request: stage immediately for hands-on testing. `Gazely.pending.exe`
now adds **Moonshine Medium (experimental)** and **Moonshine Small (experimental)**
in Speech setup. Close Gazely and run `RUN_GAZELY.bat` to activate. The default
engine is unchanged; choose Moonshine explicitly. Automatic presentation is disabled
for this experimental engine; manual Present remains available. Partials anchor
the book only, while final text supplies numbers, avoiding provisional chapter
inheritance seen in replay. No edits are made to the raw transcript.

The C# bridge streams PCM continuously into one native Python Moonshine stream;
it bypasses Whisper's utterance segmenter. Stop drains and flushes asynchronously;
session identifiers reject stale callbacks. A bounded 20-second pending queue
reports overload and stops instead of silently dropping audio. Closing the app
aborts the worker; use Stop first to finish the transcript.

Both models are installed under ignored `benchmarks/tools` (Medium 269,141,623
bytes, Small 142,300,974 bytes; combined 411,442,597). This is a local-checkout
evaluation integration, not a distributable installer. Component SHA256 locks
are checked at startup. Runtime remains moonshine-voice 0.1.5; no cloud audio.

Validation: build passed; existing 330 parser cases and supplemental suites
passed. Production C# bridge tested for two fresh sessions for **each** model:
Medium 10 finals/36 partials; Small 10 finals/40 partials; successful EOF flush,
no duplicate finals or errors. Full UI regression was not run before this urgent
staging. Performance/accuracy acceptance gates remain unpassed; see below.

Additional Medium real-time control without search also accumulated 38.38 seconds
feed lag. Thus search is not the sole cause; development-PC runtime variability
and sustained model compute still need isolation. Small has bridge validation,
not a full comparative accuracy result yet.

### Moonshine with concurrent retrieval — 8 October 2026

`replay_moonshine.py --retrieval <app-directory> --mode realtime` now starts the
installed `Gazely.Search.exe` using its production base64 query protocol. It sends
final phrases from a separate thread with one pending query (latest wins), drains
at EOF, saves query timings and raw candidates, and samples combined child-process
RAM. This is actual semantic embedding/reranking contention, but **not the entire
production quotation pipeline**: lexical lookup, phrase splitting, context retry,
WinForms dispatch, SP arbitration and adapters are excluded. No live app settings
or executables change. The script captures no microphone audio and uses no network.

The 102-second `calls-retrieval-v2` real-time run completed in 103.19 seconds
including EOF padding: 24 finals, 24 queries, zero errors and zero superseded
queries. Maximum feed lag was 0.960 seconds; sampled combined memory peaked at
1,070,174,208 bytes. Search p95 was 516 ms and queue p95 15 ms. These are process
measurements, not spoken-reference-to-SP latency or an 8 GB certification.

Semantic top matches correctly returned Leviticus 6:13 for the altar/fire phrase,
6:11 for the garment/ashes phrase, and 6:12 for the next altar phrase. Short generic
phrases also produce unrelated raw candidates; these are not automatic-live
decisions. `summarize_moonshine.py` decodes canonical corpus IDs to references.

`Test-TranscriptReferences.ps1` now records the active state for partials as well
as finals. The Leviticus chain remained anchored through 6:13, 6:14, 6:11 and 6:12.
It exposed a separate blocker: a provisional "Genesis chapter 12" survived a final
without a chapter and was combined with verses 3–4. A future Moonshine integration
must address that incorrect displayed reference; it currently retains
`RequiresReview=true`, so this trace does not demonstrate an automatic-live error.
The integration
must retain provisional book/number provenance per utterance so retracted numbers
cannot become confirmed by a later verse-only call. Do not solve this by globally
discarding incomplete references or by hiding incorrect raw transcription.

The earlier `calls-retrieval` run is invalid as a contention benchmark: the new
connector incorrectly treated an empty successful search response as an error.
That connector bug was fixed before `calls-retrieval-v2`; original evidence remains.

The installed Moonshine 0.1.5 Python stream uses the previous native decode duration
to adapt its update interval (`max(update_interval, last_pass)`, with a cap).
Consequently CPU contention can change the sequence of hypotheses, not only their
arrival time. Isolated accelerated WER must not be assumed to hold in real time;
score each concurrent real-time transcript separately.

The 240-second `genesis-retrieval` real-time run scored **24.50% WER** (110 edits /
449 words), with 62 finals and 62 completed semantic queries, no errors or query
supersession. The Matthew quotation retrieved Matthew 6:33 first. Combined sampled
RAM peaked at 1,219,817,472 bytes; search p95 was 573 ms. However maximum audio-feed
lag reached **9.99 seconds** and the run took 248.85 seconds including EOF/drain.
This configuration fails the live-speed objective despite the transcription gain.
The 23.83% isolated result is not its concurrent real-time score. Testing a 750 ms
update interval is a separate experiment, not a new production default.

`genesis-retrieval-075` scored 23.61% WER but accumulated 23.35 seconds maximum
feed lag (263.78 seconds elapsed, 1.21 GB sampled combined memory). Reject that
750 ms interval: a slightly better word score does not justify that delay.
Moonshine's upstream native runtime provides `MOONSHINE_ORT_SINGLE_THREAD=1`
([implementation](https://raw.githubusercontent.com/moonshine-ai/moonshine/main/core/ort-utils/ort-utils.cpp));
the installed DLL contains this setting. The next comparison keeps the original
500 ms cadence and applies that environment variable only to the benchmark process.

That single-thread comparison (`genesis-retrieval-single`) reduced speech-worker
CPU time to 323.28 seconds but **worsened maximum feed lag to 47.42 seconds**. WER
was 24.50%, with 62 final transcripts, 61 queries and one superseded pending query.
Reject this setting too. Smaller CPU usage is not evidence of faster inference.
Before attributing the backlog to retrieval or rejecting Medium outright, run the
same 240-second recording in real time without retrieval. These tests do not isolate
thermal/power/background-load variability on the development PC.

### Moonshine Medium evaluation — 8 October 2026

Installed `moonshine-voice==0.1.5` in the ignored `benchmarks/tools/moonshine-env`
environment. Downloaded English Medium Streaming INT8, revision
`quantized_26_08_21`, into `benchmarks/tools/moonshine-models`. The eight speech
components total **269,141,623 bytes** (about 257 MiB); this corrects the earlier
rounded 255–257 MB estimate. Optional spelling assets were also downloaded by the
vendor helper but are not enabled. No audio was uploaded and no microphone or
presentation adapter was opened. The English model/runtime uses MIT licensing.

`benchmarks/moonshine-medium-manifest.json` records component sizes and SHA256s;
`moonshine-requirements.txt` locks the isolated runtime. `replay_moonshine.py`
verifies those model hashes and runtime version, feeds original PCM in 100 ms
chunks into native streaming, captures partial/final events, pads EOF, flushes
Stop, and records worker RAM/CPU. It does not use Gazely's Whisper segmentation.
`Test-TranscriptReferences.ps1 -Events` runs captured hypotheses through the
production parser and active-reference state, but excludes wall-clock expiry,
UI, quotation retrieval and presentation. Final-only replay is insufficient to
assess this app because partial anchoring changes the context. Treat these output
references as diagnostics, not end-to-end accuracy scores.

Runs are local under `benchmarks/runs/moonshine`:

| Run | Observation |
| --- | --- |
| `calls` (102 s, accelerated) | 53.33 s compute wall time; 582 MB peak worker RAM. Correctly hears Micah and Leviticus 6:13/11/12, but misses Malachi and hears Genesis 49 as “fortunate”. |
| `calls-keyterms` (102 s, real time) | 103.12 s including EOF padding; 581 MB worker RAM; maximum feed lag 1.02 s. Six book-name hints did not fix Genesis/Malachi. No retrieval contention. |
| `calls-vad03` | Lower VAD threshold retained longer phrases but introduced stray numbers and did not consistently improve references. Not selected. |
| `calls-vad0` | Disabling VAD failed in native ONNX decoding (`node_add_10`, incompatible dimensions 20 by 56). No completed transcript or valid performance score. Rejected; keep default VAD. |
| `genesis-full` (240 s, accelerated) | 184.65 s compute wall time; 693 MB worker RAM. Qualified WER 23.83% (107 edits / 449 reference words). Correct Matthew 6:33 quotation, but a false “1 John” interrupts the Genesis call. |
| `whisper-base-control` (same 240 s, accelerated) | Current production segmentation/worker, base.en, beam 3, no retrieval. WER 40.31% (181 edits / 449 words). A fresh streaming-pipeline control, distinct from historical batch results. |
| `silence`, `noise`, `hum` (10 s each) | Zero final text, zero errors on all three synthetic fixtures with Moonshine default VAD. |

This single-recording comparison reduces raw word edits by 40.9% versus the fresh
base streaming control. It is promising general-transcription evidence, not the
held-out exact-reference error-reduction gate. Both engines receive the identical
PCM and are scored identically, but each uses its own segmentation: this measures
the proposed speech pipeline replacement, not a model-only architecture comparison.

The WER reference is the user's corrected transcript, not independently
timestamp-verified ground truth; written-number differences and the uncertain
ending count as errors. Past Whisper batch WER numbers are not matched streaming
controls. The CPU measurements are on this development PC, not certification of
an 8 GB presentation PC. These results do not demonstrate the 25% reduction gate
or Nova-3 parity. Moonshine is an evaluation candidate, not a production default;
Gazely.exe, its pending update and speech preferences are untouched by this phase.

Next gate: test default-VAD Moonshine alongside scripture retrieval, using verified
reference timestamps and held-out excerpts. Investigate the Genesis number/book
substitution before offering it as a selectable production engine. Neither name
hints nor lowering/disabling VAD earned promotion. The replay now saves partial
events and failure details for Python-level decode errors; native process crashes
can still leave only the run manifest.

To repeat locally (from the repository root, PowerShell), use the existing
isolated Python with `benchmarks/replay_moonshine.py <PCM-WAV> <model-directory>
<new-output-directory> [--mode realtime]`. Use `--keyterms` only for a separate
comparison and `--vad-threshold 0.3` to reproduce the rejected VAD experiment.

### Hearing pipeline and SP quote alternatives — 7 October 2026

The current implementation adds a 20-second PCM ring (640 KB), sample-indexed
speech jobs, and a serialized same-model refinement scheduler. It snapshots up to
16 seconds, admits only idle work estimated below 800 ms, expires recovery four
seconds after the source chunk's last voice, and permits two attempts per intent
at least three seconds apart. Session/intent identity, operator edits, release and
new references invalidate results. Book corrections can inherit the existing
numbers only through the correction parser and remain review-required. Recovery
never edits the raw transcript or triggers automatic presentation.

**Recheck unclear references (experimental)** is a persisted Speech setup toggle,
off by default. The measured development matrix admitted no refinement jobs on
this PC: its conservative cost gate protected live speech, but it has not shown
an accuracy gain. Do not enable it by default without new measured evidence.

A repeated leading verse number can no longer become a missing chapter. The
matrix also exposed a clean numeric call after "Leviticus is. 6th verse 13";
a narrow book-tail grammar handles that without replacing ordinary words.

The latest user addition supersedes automatic same-book quotation completion:
strong unambiguous lexical quotations (score >= .94) appear as smaller muted SP
alternatives, leaving the spoken anchor in place. Clicking a quote selects it for
review and exposes a return-to-spoken-anchor button. Speech continues updating
that anchor while the operator holds the quote. Up to 20 alternatives remain in
the scrollable SP list; all suggestions remain in History. Clearing Detections
also clears this list, preserving History. Compact windows can scroll the reading
area instead of clipping scripture text. Selecting an alternative does not present.

`benchmarks/Run-HearingMatrix.ps1` runs sequential normalization/context/beam/VAD
ablations using the installed models and concurrent semantic retrieval. EOF padding
now follows the configured endpoint. Twelve accelerated variants completed with
zero inference errors or dropped final windows; see
`benchmarks/hearing-matrix-results.csv`. Accelerated timing is NOT live latency,
and reference output counts are NOT accuracy. Base 10-second windows recovered
Leviticus 6:11/12; quantized small recovered 6:13 too, but took roughly 6–8 seconds
per final inference. Neither reliably recovered the Malachi/Micah correction.
Silero degraded the selected reference excerpts and remains disabled.

`prepare_hearing_fixtures.py` preserves local source hashes and separates phase
development excerpts from reserved excerpts; silence/noise/hum fixtures are seeded.
`score_reference_events.py` excludes unverified annotations and reports no live
latency for accelerated runs. The annotation template has unverified approximate
timings: do not claim the 25% error-reduction gate without checked audio labels.
The existing Genesis user reference supports qualified whole-recording WER only.

An additional MIT Distil-Whisper candidate was downloaded and SHA256-verified by
`Get-CompactSpeechCandidate.ps1`: 336,191,657 bytes; base + distil + VAD totals
485,040,966 bytes. This is an evaluation candidate, not a production UI profile.
Source: https://huggingface.co/distil-whisper/distil-small.en (pinned revision and
checksum in the downloader). Bench models are not automatically bundled together.

Its sequential 102-second accelerated replay (`distil-calls`) completed without
errors or dropped finals, but missed Genesis 49:3–4 and the Malachi/Micah correction.
It emitted Leviticus 6:13 and 6:12; final inference p95 was 3,408.90 ms. This is
insufficient evidence to replace base.en, and the candidate remains evaluation-only.
No new speech model or decoder/window default is promoted in this update.

The UI suite `benchmarks/runs/ui-hearing-release` passed quote/anchor
swapping, further spoken navigation, retaining other quotes, stale recovery,
no unintended presentation, layout and preferences. Parser, quotation and 31 audio
pipeline checks also passed.

The 30-minute real-time replay (`benchmarks/runs/hearing-phase/stress-30min`)
completed all 277 final windows with zero inference errors, zero dropped finals,
and at most one waiting job. Final-readiness p95 was 1,919.78 ms from the window's
last voice; queue p95 was 813.40 ms. These are transcription measurements, not
correct-reference display latency. Only two explicit detections were emitted:
reliability and timing do not establish recognition accuracy. Sampled combined
working set peaked at 1,377,771,520 bytes for replay, speech and retrieval workers.
The harness retained completed audio keys during this run (subsequently fixed),
and some UI regression checks ran alongside it. This is not an isolated benchmark
or validation alongside presentation software on an actual 8 GB PC. No refinement
jobs were admitted. Held-out accuracy and the 25% improvement gate remain unproven.

Fixed 10-second silence, seeded noise and hum replays with retrieval produced zero
explicit scripture calls and no errors/dropped finals. Silence emitted no speech.
Noise still hallucinated ordinary text ("Let's go", "that's it") and hum emitted
"you": noise rejection is not solved. These synthetic checks are not a replacement
for annotated real background-noise recordings. The tested candidate is staged
through `Gazely.pending.exe`; restart with `RUN_GAZELY.bat` to activate it while
retaining the previous executable. This delivers the guarded functionality, not
acceptance of the speech-accuracy targets. Checked audio labels, held-out accuracy,
real reference-display latency and an actual 8 GB presentation-PC trial remain.


### Incomplete final transcripts must not erase SP — 7 October 2026

The reported 09:09 log contains no final chapter/verse at the Leviticus call.
Previously finalization dropped the partial reference and restored book-only SP.
`ActiveReferenceState` now keeps a separate review-required retained hypothesis.
Less-complete same-book updates cannot downgrade a resolved reference. Explicit
new references replace it, and Release scripture clears all three states.
Retained numbers are not confirmed evidence: the UI marks them Unconfirmed, and
presentation policy rejects them for automation. Manual review remains available.
Contextual `go to VES 11` / `start from VES 11` can recover verse navigation only
with an existing book and chapter, always requiring review. No global word-to-number
or `west` substitution was introduced. Raw transcription remains unchanged.

Verification: parser suite, desktop build, and full UI regression passed in
`benchmarks/runs/ui-retained-reference-1`, including the reported incomplete-final
sequence, competing quotes, contextual VES, explicit replacement, release and no
unexpected adapter actions. This fixes selection persistence, not acoustic accuracy.

### Active-reference SP state machine — 6 October 2026

`ActiveReferenceState` separates confirmed and provisional scripture intent, with
Searching, Book identified, Chapter identified and Reference resolved stages.
Credible partial book calls appear immediately. Incomplete revisions now retain the more complete hypothesis as Unconfirmed (see 7 October correction above). Both speech backends use this state.
The confirmed or retained state seeds parser context before each new transcript, so normal
pauses and explanations do not expire the active book/chapter. Standalone parser
expiry rules remain unchanged when no active state is supplied.

SP remains locked against quote-driven replacement, including during book/chapter
drafts and after corrected references. Quotations still populate Detections and
History. New explicit references, contextual verse instructions, corrections and
operator actions remain available. Release scripture replaces the former Follow
speech action and clears both operator hold and active intent. Stop/start resets
the active context; clearing Detections alone does not release it.

A bounded implicit switch is supported only for explicit transition phrases
(elsewhere Scripture/Bible says, another scripture/passage says, in another place
it says) plus a current-text lexical quotation score >= .94 with no shared-wording
ambiguity. It does not use semantic cosine as a switch probability. The quotation
remains review-required and cannot automatically present. Other implied transitions
remain operator decisions. No speech-model or acoustic settings changed.

This supersedes timed SP protection and earlier tests expecting Isaiah/Micah quotes
to complete or replace an active reference. Those quotes must now reach Detections
without taking SP. UI coverage also checks partial withdrawal, book/chapter/verse
progression, aged active state, explicit book switching, a supported quote transition
and release. The final regression evidence is `benchmarks/runs/ui-active-reference-final`.

### Leviticus selection stability — 6 October 2026

[Recording findings](benchmarks/LEVITICUS_SAMPLE_FINDINGS.md) document the 13–16
minute excerpt and same-server longer-context experiment. Production ASR defaults
remain unchanged. Parser now retains bounded explicit book requests across filler,
handles chapter-slot ordinals and additional contextual verse instruction forms.
Recent explicit/contextual/navigation selections are protected from quote replacement
for 90 seconds; this supersedes the earlier ten-second automatable-only guard.
Candidate publication and manual/new spoken reference changes remain available.
Full UI replay verifies the reported 6:13 → 6:11 → 6:12 flow against competing
quote results, alongside existing regression cases. Selective audio re-decoding
remains an experiment to implement, not a shipped feature.

### Malachi / Micah correction and replay — 2 October 2026

See [sample findings](benchmarks/MICAH_SAMPLE_FINDINGS.md) for the actual recording
comparison. Base and small both failed the explicit call in live replay; small was
slower. No speech default or model changed. Book-only explicit corrections now
retain recent valid numbers as review-required evidence and update quote scope.
Parallel lexical quotations retain a bounded review shortlist rather than hiding
all near-tied passages. Page 1 contains newest detections after pins; Latest returns
there without changing the policy that protects an operator browsing older cards.
These changes do not claim to fix the recording's acoustic transcription errors.

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

# 8. CLOUD ACCOUNT / BILLING IMPLEMENTATION (2026-10-15)

The user approved Supabase Auth/Postgres, Paystack subscriptions, an offline
signed desktop licence, and a small Vercel customer portal. The older
"Supabase deferred" note is superseded for this work. React remains deferred
in the Windows app. No tools, models or runtime packages were downloaded.

## Test environment now

- Supabase **Gazely Presenter** (`bpafhxoebexyhugsvfiq`) is explicitly the
  **test** project. There is no production Supabase project yet.
- Versioned migrations `202610150001` through `202610150003` were applied to
  test. `supabase/tests/accounts_security.sql` passed inside a rolled-back
  transaction: client grants, church scoping, Free device limit, usage
  idempotency, payment replay, cancellation and refund transitions.
- `gazely-api` is deployed to test at version 5; `paystack-webhook` at version 4.
  Missing JWT and forged webhook requests each returned HTTP 401. A renewal
  without an embedded subscription code is matched only after Paystack
  verification against the stored customer, plan, currency and amount.
- The `gazely` PostgREST schema is exposed via a versioned authenticator-role
  setting; an anonymous query returned HTTP 401 / permission denied. The
  Supabase RLS advisor reports eight INFO notices for tables with RLS and no
  policies. This is intentional: clients have no grants and the service-only
  API mediates access. Avoid adding permissive table policies.
- The test project's public URL and publishable key are in ignored local
  `portal/config.json`. No secret keys are in source. The user pasted an old
  Paystack **test secret** in chat; it must be rotated before use. It was not
  placed in code or Supabase.
- A test-only RSA signing pair was generated locally in ignored
  `supabase/.secrets-test/`. Never reuse it for production. The private key
  has not been installed in Supabase.
- `Gazely.mvp-candidate.exe` compiles with an Account page, OTP/activation
  client, local signature verification, DPAPI storage, and 15-second monotonic
  usage checkpoints. The candidate is still an evaluation build because no
  signed lease can be issued until test service secrets and portal origin are
  configured. `-CloudRelease` bakes public settings into the EXE and enables
  the listening gate; the public package builder refuses a candidate without
  a matching cloud-release marker. No installer was built or replaced.
- The portal is a dependency-free static app in `portal/`; `node build.mjs`
  and `node --check` passed. It includes a Paystack hosted payment-management
  link for active subscriptions. It has not been deployed. A GitHub remote already
  exists: `IsraelPhilips/Gazely`, but these portal changes are uncommitted.

## Required test-project setup (owner/service dashboard)

1. Rotate the exposed Paystack test secret. In **test** Supabase Edge Function
   secrets, enter the replacement as `PAYSTACK_SECRET_KEY`, plus
   `PAYSTACK_MODE=test`. Never paste the replacement into chat or Git.
2. Create four Paystack **NGN test plans** with exact kobo prices: Africa
   monthly `250000`, Africa yearly `2500000`, outside Africa monthly `665000`,
   outside Africa yearly `6650000`. Paystack's yearly plan interval is called
   `annually`. Set their codes as
   `PAYSTACK_PLAN_NGN_AFRICA_MONTHLY`, `...AFRICA_YEARLY`,
   `...GLOBAL_MONTHLY`, and `...GLOBAL_YEARLY`. USD prices are prepared in
   source but checkout remains disabled until merchant confirmation and full
   USD test flow.
3. Set `GAZELY_LICENSE_PRIVATE_KEY_PKCS8_BASE64` from the ignored local test
   private-key file and `GAZELY_LICENSE_PUBLIC_KEY_SPKI_BASE64` from the
   ignored public-key JSON. Set `GAZELY_PORTAL_ORIGIN` to the exact eventual
   test portal HTTPS origin. Supabase automatically supplies its project URL
   and legacy service/anon keys; a publishable key can also be set as
   `SUPABASE_PUBLISHABLE_KEY`.
4. In Paystack **test** webhook settings, use
   `https://bpafhxoebexyhugsvfiq.supabase.co/functions/v1/paystack-webhook`.
   A checkout return does not grant Pro; only a signed event plus Paystack
   reconciliation does.
5. Configure Supabase Auth email OTP to send the actual `{{ .Token }}` code,
   allowed portal redirect URLs, and custom SMTP before public testing.
   Resend is the default proposed provider. Production needs its own project,
   sender domain, keys, SMTP, plans and webhook.
6. Vercel portal project should use GitHub repo `IsraelPhilips/Gazely` with
   root directory `portal/`, after the verified portal files are committed and
   pushed. Set public build env `SUPABASE_URL` and
   `SUPABASE_PUBLISHABLE_KEY`; `node build.mjs` creates `dist/config.json`.
   The portal has no npm dependencies. Obtain its HTTPS origin before setting
   Supabase CORS/callback and cloud-release public config.

## Still unverified / release gate

The test project has no production SMTP, Paystack secret/plans, portal origin
or signing secret configured yet. No OTP → activation, genuine Paystack
checkout/webhook, renewal, refund, offline lease, usage reconciliation or
portal/desktop matching-status end-to-end run has passed. Windows DPAPI
roundtrip passed outside the sandbox with nonsecret bytes; the full desktop
account flow still needs a configured test service. Run the acceptance suite,
then build a cloud-enforced candidate, isolated installer, code-sign and smoke
test. Existing production/pending/rollback builds must remain untouched.
In particular, exercise a failed renewal followed by a fresh checkout and
confirm the old Paystack subscription cannot charge again; this provider
sequence is not yet proven and must block public checkout until resolved.

---

# 9. FREE-LAUNCH ACCOUNT POLICY (2026-10-17)

The user changed the launch policy: **every signed-in church gets all current
features free**. Accounts and device activation remain required. This
supersedes the earlier launch pricing/Free-limit policy until an administrator
activates billing. Pricing, Paystack reconciliation and paid entitlements stay
in source for a later paid launch, but checkout is refused while billing is
disabled. Do not configure or enable public Paystack checkout for the free
release.

- Test Supabase migrations `202610170001` and `202610170002` are applied.
  `gazely.release_settings.billing_enabled` is `false` by default and has no
  client grants. The test project still has `billing_enabled=false` after the
  rolled-back checks. Only an administrator can change it in Supabase SQL.
- Free launch issues a signed, device-bound seven-day lease with unlimited
  listening, automation, Mini and manual presentation. Two devices are
  allowed per church. No audio or transcript is uploaded. Old paid-mode Free
  semantics (one device and 3,600 seconds) apply only after billing is on.
- The portal hides its billing controls during free launch. `gazely-api`
  refuses checkout even if a client calls it directly. Test deployment:
  `gazely-api` version 6. `paystack-webhook` remains version 4.
- The billing switch is one-way. On activation, it starts a new server usage
  anniversary so previous free-launch listening does not consume the paid-mode
  Free allowance. Old signed leases can continue offline for at most seven
  days and their delayed usage reconciles into the old period. Plan the switch
  with that seven-day transition in mind.
- `supabase/tests/accounts_security.sql` passes in a rolled-back transaction
  for launch-free two-seat access, future paid-mode one-seat Free access,
  cross-church denial, old/new usage periods and payment transitions. A local
  mocked Edge Function call confirmed authenticated checkout is refused before
  Paystack, and status tests covered launch-free, billed Free and Pro. The
  Windows signed-lease test, candidate compilation and portal build pass.

To activate billing **later**, after Paystack and the paid flows pass their
acceptance tests, an administrator can execute in the production Supabase SQL
editor: `update gazely.release_settings set billing_enabled=true where
singleton=true;`. The trigger records the transition time. Do not perform
that action during the free launch. Turning billing back off is deliberately
rejected by the database trigger.

The free public release still needs production Supabase, custom SMTP/OTP,
production licence signing, portal deployment, an authenticated desktop-to-
portal test, and a signed installer smoke test. The existing evaluation
candidate is not a cloud-enforced public build. No installer was built here.

## Free-launch email entry change (2026-10-17, latest)

The user deferred OTP and Google sign-in. The current candidate requires an
email address before listening or opening Mini. It sends the address and a
random installation ID to the `gazely-email-entry` Edge Function, waits for
confirmation, then stores the address with Windows DPAPI for subsequent offline
starts. The address is **unverified**; it is not a Supabase Auth user or church
membership and grants no licence, billing, device-seat or portal rights. The
user can enter any syntactically valid email. A new installation needs network
access for its first submission; existing submitted installations work offline.

`gazely.email_entries` is a private, RLS-enabled service-only table in the
**test** project; migration `202610170003_email_entries.sql` was applied and
`gazely-email-entry` version 1 deployed. The public endpoint validates input
and records only email, installation ID and timestamps. Public endpoints can
still receive spam; add abuse controls before substantial public traffic.
Anonymous client roles have no table access. No sermon audio or transcript is
uploaded. An email entry cannot enforce church/device limits; those depend on
verified sign-in later.

`Gazely.mvp-candidate.exe` now uses `-EmailEntry` with the test project's
public URL/key. `Test-EmailEntry.ps1` passed first-use block, server submission,
unlock and protected local reload. A malformed email was rejected by the
endpoint. Test entries were removed. Packaging and installer guards reject
this test-project build. For a public installer, provision a separate production
Supabase project, apply the migration and deploy the endpoint, build with its
public config, then run an isolated installer smoke test. The user has asked
not to build the installer yet. OTP, Google sign-in, portal account management,
Paystack checkout and paid access remain deferred. The earlier free-launch
signed-licence path remains in source for later verified authentication.

## Final-installer request status (2026-10-17)

The user authorized building the final installer. A separate production
Supabase project, `Gazely Presenter Production` (`kbuiyukrcxxotpksryqt`), was
created in the selected organization after Supabase quoted $0/month. The
private `email_entries` migration is applied there. The email-intake function
is **not deployed to production**. Automatic approval review rejected a public
function using service-role writes because of persistent abuse and data
integrity risk; it also rejected the attempted narrower anonymous table-insert
design because it would permit unbounded spam/storage writes. No rejected
deployment or anonymous grant was applied. The tested intake remains on the
test project only. Do not build or label a test-project candidate as final.

Both speech packages passed installed/offline status checks, and the interface
suite completed successfully in `benchmarks/runs/ui-email-release-20261017`.
No usable code-signing certificate was found in CurrentUser or LocalMachine
personal stores. The release still needs an approved production email-intake
path, a production-configured candidate, packaging, isolated installer smoke
test, and a decision on code signing. No installer has been built.

## Offline free-access release decision (2026-10-17, latest)

The user removed the sign-in/email gate for the first public build. Gazely now
opens the Live workspace and first-run guide with no account or internet
connection. Listening, Mini and existing automation are available without a
cloud licence. The Account navigation item is hidden in this release; the
backend, migrations, portal, payment and verified-auth code remain in source
for later work. No personal data is submitted to Supabase by this build.

`Gazely.mvp-candidate.exe` was rebuilt with `-FreeAccess` and has a matching
`free_offline`/`offline` release marker with no Supabase or portal URL.
`Test-FreeAccess.ps1`, the signed-lease unit checks and the complete interface
suite (`benchmarks/runs/ui-free-release-20261017-r2`) pass. Installed Whisper
and Nemotron packages passed offline verification. Previous installers in
`releases/` remain intact. The user approved preparing an unsigned installer
for review; a trusted publisher signature can be added later.

The new offline package is `releases/Gazely-Windows-Free-20261017.zip`
(SHA-256 `D239CEF731EE0088408619BC435A792965161F69D01ADD78273443E608FA70FB`).
The unsigned installer is `releases/Gazely-Setup-Free-20261017.exe`,
997,724,255 bytes, SHA-256
`1AA3D7A1EBD5A560F6A7C2A5B8C1D9308E9912D413DF077861146ED1B70AE5F9`.
An isolated `--quiet --no-shortcuts` install returned exit code 0; its 364-file
manifest and installed EXE matched the candidate. The isolated test folder
was removed after verification. No older installer or installed Gazely build
was changed. This build is unsigned; Windows can show Unknown publisher.

## Installer and live-selection follow-up (2026-10-17)

The operator reported an older Gazely installation absent from Windows Installed
apps, repeated Bible-version top-to-row scrolling, and a newly spoken reference
remaining beneath an operator-selected main passage. The old installer created
a per-user versioned folder and shared Start Menu shortcut without registering
an uninstaller. Its installation is unchanged. The new installer registers its
own Installed apps entry with a guarded per-version uninstaller; settings and
history outside the versioned folder are preserved.

Manual EasyWorship actions now reuse a completed same-process version position
when the read-only selected-row probe is unavailable. A readable row still wins;
automatic output still requires fresh verification. Unknown position, process
change and explicit Reset retain the conservative top re-anchor. Complete spoken
explicit/correction/contextual/navigation references now replace operator-picked
quotes, history rows and completed drafts in the main SP panel; the prior
selected passage stays below. In-progress edits still hold the panel.

Navigation and adapter tests passed (24 and 35 checks), and the full free-access
interface suite passed in `benchmarks/runs/ui-free-release-20261017-r4`, including
new operator-selection regression. Candidate rebuilt with `-FreeAccess`.
New archive `releases/Gazely-Windows-Free-20261017-r2.zip` has 365 manifest
files; new unsigned installer `releases/Gazely-Setup-Free-20261017-r2.exe` has
SHA-256 `9357D1520F60FF436838C9D175E8FAB52AD28D09523BAC7496B92E79084687AA`.
Isolated no-shortcut install returned exit 0 and installed EXE hash matched the
candidate. EasyWorship had no exposed main window during this turn, so real
library readback and live presentation remain unverified. Existing packages and
installed builds were not replaced.

## Daniel/SP and automatic-output clarification (2026-10-17)

The operator clarified that EasyWorship opening without projection happened on
the previous install; automatic Live has not yet been tested on r2. In the new
screenshot, the footer specifically says requested KJV is not mapped; the
operator then confirmed the Bible versions had not been saved. No current-build
automatic presentation failure has been established. Fresh selected-library
verification remains required before any automatic output.

The actual transcript `daniel chapter two and verse forty six` failed the
parser because `TryParseChapterVerse` did not allow `and` immediately before
an explicit verse marker. Quotation search separately found Daniel 2:46,
explaining why it appeared only in Detections while SP showed Daniel. The
parser now accepts that narrow conjunction. Exact Daniel and split
`hebrews chapter eleven` / `and verse thirty two` cases pass parser and full
interface tests (`benchmarks/runs/ui-free-release-20261017-r5`). The split
Hebrews result reaches SP but remains review-required for automatic Live; this
is intentional until live operator feedback supports a narrower safe promotion.

Candidate rebuilt in offline free mode. Separate archive
`releases/Gazely-Windows-Free-20261017-r3.zip` and unsigned installer
`releases/Gazely-Setup-Free-20261017-r3.exe` were created without changing
earlier builds. Installer SHA-256:
`DCD87AD34C7F0FB82023A490EA41BBF2CAA13385BA255CFCB81EDF9EC3351A16`.
An isolated no-shortcut install returned exit 0; its 365-file manifest and
installed EXE matched the candidate. The smoke folder was removed.

## Automatic EasyWorship library readback (2026-10-17)

The operator supplied the decisive log: automatic Isaiah 53:1 found EasyWorship
and activated Scriptures, then stopped with "library translation is not
readable" before any live Enter. The Bible map includes KJV=9; map position is
not proof of current selection. Manual Present succeeds because it is an
operator-authorized action that can use a calibrated position without a fresh
library readback. This is the precise auto/manual behavior difference. The
Integrations `Check library selection` result showed "UIA exposed no uniquely
selected translation"; the prior MSAA child-state fallback also found none.

The read-only MSAA fallback now checks `accSelection` directly before scanning
child selected-state flags, per Windows accessibility semantics. A standard
Windows selected list test and the full free-access interface suite passed in
`benchmarks/runs/ui-free-release-20261017-r8`. EasyWorship's custom list could
not be exercised from this execution session, which sees process handles but
no interactive app windows. Do not claim its readback or automatic projection
is fixed until the operator tests this candidate. If it remains unreadable,
automatic output must stay paused; inspect the new specific probe result and
develop another reliable read-only selection signal. Never treat the live KJV
slide or resource filenames as proof of library selection.

Candidate rebuilt in free offline mode. A separate unsigned
`releases/Gazely-Setup-Free-20261017-r4.exe` (SHA-256
`49962B552C919EAE161DFE4C4CC8350891E43B2CDA38D6ABB347078D419136E1`)
passed a 365-file isolated no-shortcut install and installed-EXE hash check.
Earlier installers and installed versions remain intact.

## KJV-first tracked automatic presentation (2026-10-17, latest)

The user explicitly chose KJV-first operation and accepted that direct Bible
changes inside EasyWorship can make Gazely's tracked position stale. Manual
Present and automatic Live now call the same EasyWorship version-selection and
reference-entry path. Both require the requested version in the saved local
map. A readable library row is used as the origin and a readable mismatch
stops the send; an unreadable row uses the mapped/tracked position rather than
blocking automatic output. The old automatic-only readback checks before live
Enter were removed. The existing reference-control readback still must match
before either path sends Enter. Speech/quotation review policy is unchanged:
not every SP candidate is eligible for automatic Live.

The saved map seeds an assumed KJV position at process attachment. Repeated
KJV sends do not scroll. Explicit Reset version tracking discards the position
and causes a top re-anchor on the next send. Because external EasyWorship
changes are not visible, operators should use Reset after switching versions
directly there. Do not describe a successful command as verified audience
output. UI copy and package README now describe this policy.

Parser, 24 navigation, 33 adapter, free-access and full interface tests passed
(`benchmarks/runs/ui-free-release-20261017-r11`). Candidate rebuilt in
`free_offline` mode. New unsigned installer
`releases/Gazely-Setup-Free-20261017-r5.exe` SHA-256
`615A42B3A248085B4C64423C8C1F6BF549D6C57D784D11C4883AE763B0084390`
passed isolated no-shortcut installation: 365-file manifest, installed EXE
matching candidate. No EasyWorship live action was run from this execution
session, so automatic projection still needs operator confirmation. Older
installers and installed builds remain intact.

