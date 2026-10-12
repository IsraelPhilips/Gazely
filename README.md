# Gazely

**Intelligence for live presentation.**

Gazely pays attention. So you can stay present.

Gazely is an intelligence layer for existing presentation software. Its first
milestone is offline scripture assistance for EasyWorship 7. Songs, lyrics,
presentations, media and other live workflows are future capabilities, not
features of the current prototype.

## Read these documents

- [Technical guide](TECHNICAL_GUIDE.md): codebase onboarding for engineers, models, retrieval internals, process boundaries and development workflow.
- [Product vision](PRODUCT_VISION.md): product boundaries, hardware goals, architecture and roadmap.
- [Execution plan](EXECUTION_PLAN.md): next-phase milestones, contextual navigation rules, acceptance targets and delivery order.
- [Engineering handoff](ENGINEERING_HANDOFF.md): implemented behavior, evidence, limitations, tests and preserved EasyWorship discoveries.
- [Brand guidelines](Gazely_Brand_Guidelines.md): authoritative identity, naming, messaging and visual guidance.

## Current status

The v6.3 Windows prototype contains audio capture, local Whisper transcription,
scripture interpretation and EasyWorship 7 UI automation. It is **not yet a
production release**. Real-sermon testing showed severe transcription errors
and unspoken Bible-book hallucinations. The same audio performed much better in
Pewbeam, which the user assesses at roughly 98% accuracy. Improving offline
recognition on ordinary hardware is the immediate engineering priority.

The preserved baseline displays its historical Scripture Assistant name; the
latest evaluation build identifies itself as Gazely. It now has a dark/light
workspace using the brand palette, with separate setup and integration screens.
The approved visual direction now extends across all seven sections: compact
rounded input fields, soft surfaces and light/dark themes. Recent scriptures sit
inside Live workspace with direct Prepare/Present actions. The Present page also
shows a local KJV preview, explicitly separate from the selected output version.
The supplied PNG brand symbol and Windows app icon are included. Onboarding,
model installation and high-DPI/accessibility validation remain outstanding.

Choose **Edit reference** in Live workspace to open the book/chapter/verse controls.
Choose **Mini overlay** in the navigation for the live-service view. On first use,
select the operator monitor (Windows **Extend**, not Duplicate/mirror). Drag the
title or resize the edges; use the square button or `…` menu to switch between
Collapsed, Compact and Expanded. Expanded includes live transcript, automation
toggles and paged detections with direct live actions. `↗` opens the full workspace.
The `…` menu can anchor to an open EasyWorship/ProPresenter window; it follows that
window and hides when the target minimizes or leaves the selected operator screen.
A Gazely tray icon restores the workspace if the target closes. Position, size,
mode and monitor choice are saved. The overlay briefly hides during automation
so it cannot cover controls the adapter needs. Anchoring to ProPresenter does not
add a ProPresenter presentation adapter; output still uses the configured working
integration. Mirrored displays or external capture of the whole operator screen
can include the overlay; use a separate presentation output.

The book field supports both typing with autocomplete and selection
from all 66 books. Enter chapter/verse (or a range), then **Use edit** to hold the
validated reference. **Release scripture** clears the operator hold and active
scripture so SP can search again.

Selected passage (SP) follows an active scripture: recognized book → chapter →
verse. Partial speech is visibly provisional; a more complete hypothesis survives an incomplete final as **Unconfirmed**, requiring review before presentation. Confirmed context persists through
pauses. Quotes keep collecting in Detections without replacing that context.
A new explicit reference can switch it immediately. Supported transition phrases
plus a strong literal quotation may switch SP, but that quotation still requires
operator review before presentation. Similarity alone cannot switch an active SP.

Spoken translation requests such as **“give me that in NLT”** or **“use the
Message”** update the active complete passage for review. Verse continuations and
“next verse” retain its requested translation; a new full reference defaults to
KJV unless another version is explicitly requested. The target translation must
be installed and mapped in EasyWorship. Gazely's local passage text remains KJV,
labelled **KJV preview (output: NLT)** when the requested output differs.

Strong quotation alternatives also appear beneath SP in a smaller, muted list.
Click one to review it as the main passage; the spoken reference remains available
underneath and continues following speech. Returning to it keeps the other quotes.
Selecting a quote does not send it live. Speech setup includes an optional
**Recheck unclear references (experimental)** toggle, off by default; its bounded
audio buffer stays in memory. Current measurements do not justify changing the
production speech defaults. See the engineering handoff for results and limits.

For the latest parser and offline quotation work, run
`EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/Gazely.exe`.
It detects same-chapter ranges and contextual verse instructions, and offers
review-required quotation suggestions with local KJV evidence. Keep the `corpus`
folder beside the executable. Start with Automatic Live off. The original launcher
still opens the preserved baseline. Recognition accuracy remains below target;
this is an evaluation build, not a production replacement.

## Test the current Gazely build

The October 12 candidate adds **Speech setup → Nemotron English (experimental)**
using the locally installed English Q8 model and CPU runtime. It uses one
continuous streaming session; no audio goes to an online transcription service.
Whisper remains the default. Experimental streaming engines require manual
Prepare/Present, including from Mini overlay. Close Gazely and launch
`RUN_GAZELY.bat` to activate a staged update; switching back to Local AI restores
the existing Whisper path. Model/runtimes are local evaluation assets, excluded
from Git; this is not yet a self-contained installer.

### Build from a Git checkout

The repository includes source, tests, brand assets and the KJV/WEB text corpus
with provenance and notices. Executables, downloaded models/runtimes, recordings,
raw benchmark runs and machine-specific settings are excluded. Historical local
evidence and executable links in the documents may therefore be unavailable in a
fresh clone. Existing local installations are not removed by these exclusions.

On Windows with Windows PowerShell and .NET Framework installed, build and run:

```powershell
Set-Location EasyWorshipController_MVP_v6_2_CODEX_HANDOFF
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-EasyWorshipController.ps1 -OutputFileName Gazely.exe
.\RUN_GAZELY.bat
```

For offline speech, run `SETUP_LOCAL_WHISPER.ps1 -Model base.en` with Windows
PowerShell; this setup downloads the speech runtime and model. For optional
semantic quotation search, install the .NET 8 SDK and run
`SETUP_SCRIPTURE_SEARCH.ps1`, which downloads the compact models, restores build
dependencies and publishes the local worker. Setup requires internet access;
recognition and search then run locally. Python is only used by development
benchmark tools. Keep corpus notices with any distributed build.

Theme and automation preferences, integration choices and Bible-version mappings
are local to each installation. Fresh installations use application defaults.

### Try the application

1. Run [RUN_GAZELY.bat](EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/RUN_GAZELY.bat).
2. Open **Speech setup** and select the microphone/mixer, Local AI and profile.
3. Open **Integrations** to enable EasyWorship 7. The bundled adapter needs no
   download; Gazely remembers whether it is enabled. Later integrations are
   listed as planned, not working options.
4. Return to **Live workspace**, leave Automatic Live off, and start listening.
5. Use **Evidence** to inspect a suggestion, **Prepare** to fill the target's
   scripture selector, and **Present** to request live output. Check EasyWorship's
   screen: Gazely does not yet verify actual live output.
6. **History** keeps suggestions and their source evidence for this app session.
   Its percentages are heuristic match scores, not measured certainty. Provisional
   revisions update one row and receive a final score only when confirmed.
   The **Scripture candidates** panel retains session suggestions across pages.
   Use **Expand list** and Find to manage several passages. Each row has a play
   icon for immediate Present, a Prepare icon, and a menu for Pin / Dismiss.
   Dismissal preserves the evidence in History. The Detections section's options
   menu opens **All history** and **Reset version tracking**.
   Select an entry to hold it for review without sending it. Prepare and
   Present act on that saved reference; incomplete/unconfirmed entries must first
   be completed or confirmed in the editor. **Release scripture** releases the hold.

Offline meaning search is now installed on this machine: compact BGE embeddings
and a MiniLM reranker search KJV and World English Bible wording, returning canonical
references with KJV previews. The two model weights total 57.2 MB; runtime, models,
indexes and corpus currently occupy about 252.5 MB (241 MiB), excluding speech
recognition and developer tools. No Qwen model was downloaded. Python is not needed.
The optional comparison-model download failed and is not part of the application.

Meaning matches are **possible paraphrases**, never automatic-live instructions.
Author-written text tests found the intended verse in 6/11 harder paraphrases and
14/15 in a separate set, with some additional incorrect suggestions. This is not
real-sermon accuracy, calibrated certainty or proof of arbitrary paraphrase coverage.
Lexical search continues if the semantic package is missing or fails. Search-worker
failures have a one-minute retry cooldown. Setup/installer UX and 8 GB validation
remain unfinished. See the engineering handoff for reproducible results.

Close Gazely and reopen with **RUN_GAZELY.bat** after an update. If the app was
running during the build, this launcher activates the staged update automatically.

Gazely now remembers theme, Auto prepare / Automatic Live, speech engine, model,
voice pickup, selected input (when still available) and early-result display in
`gazely-preferences.txt` beside the app. Changes save as you make them and on exit.
Previously unsaved choices must be set once in the updated build. Listening always
starts stopped; enabling Automatic Live is confirmed on first selection, while a
saved enabled choice restores without asking again. Detections/History remain
session data. Existing integration enablement is saved separately.

The workspace input meter shows audio activity without flashing voice/no-voice
warnings during normal pauses. **Live text (revising)** displays partial recognition
immediately; committed transcript entries still wait for the final recognition result.

If a clear scripture call contains a misheard chapter phrase, Gazely can propose
a pronunciation-based chapter candidate using an installed English Windows voice.
For example, “Genesis chapter for tonight” can suggest chapter 49 when followed
by a verse cue. Ambiguous verse numbers remain unresolved and cannot be presented.
These suggestions always require review; this is not general paraphrase reasoning.

Spoken ranges now start at their first verse: “Genesis 49:3–4” selects 49:3.
A clear “next verse” following a recent unambiguous reference can use Auto prepare
or Automatic Live when that mode is enabled. Clear numeric calls such as “Genesis
one one” also qualify. Quotes, paraphrases, pronunciation recovery and ambiguous
navigation still require review. A manually entered range remains a range.

Chapter-only calls such as “Isaiah 51…” stay as incomplete drafts. Recent chapter
context helps quotation selection; a bounded two-chunk semantic retry can recover
readings split by transcription. Selecting a history entry or editing a reference
holds the main passage: use **Release scripture** to release that hold.

**Clear** at the top of Detections asks for confirmation, hides existing detections
and keeps History. Results already in flight cannot repopulate the cleared panel;
new speech can create new entries. Each card separates the KJV passage from the
smaller “Heard” text. Hover or open the passage to inspect truncated text.

If an update was built while Gazely was running, close its window and open the
launcher again. It activates the staged build and keeps `Gazely.previous.exe` as
the previous version. No service-time restart is forced.

Stop cancels pending adapter work. Escape stops listening and disarms automation.
Integrations also provides cancellation;
disabling/re-enabling never revives an earlier pending command. A command already
sent to the presentation app cannot be undone by cancellation.

## Run the existing prototype

**Archived speech evaluation:** [Gazely-Phase2.exe](EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/Gazely-Phase2.exe)
contains measured prompt-removal and repeated-output safeguards. Close the old
controller before launching it. Use Local AI / Balanced / Normal with Automatic
Live off for testing. See [Phase 2 findings](benchmarks/PHASE2_PROGRESS.md);
accuracy remains below target and scripture range handling is not fixed yet.
The updated evaluation build also supports **More accurate** using the installed
compressed small model. It improved this sermon's word error rate to 30.7%, but
had a 5.44-second p95 ready delay. Balanced remains the faster default; no new
download or setup run is needed on this machine to try More accurate.
The existing launcher below still starts the preserved baseline executable.

Files remain in [the existing application folder](EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/) to preserve script, model and runtime paths.

1. Close older Scripture Assistant windows.
2. Open EasyWorship 7.
3. Run [RUN_CONTROLLER.bat](EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/RUN_CONTROLLER.bat).
4. Choose the intended microphone/mixer, Local AI, Balanced and Normal voice pickup.
5. Keep Automatic Live off while evaluating recognition. Start listening.

A trailing `?` marks an unfinished candidate. Finalized interpretations appear
under Detected Scripture. Number-format and phonetic suggestions require review.
These settings are an evaluation baseline, not validated production defaults.

To rebuild the current evaluation executable, run the following from its application folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Build-EasyWorshipController.ps1 -OutputFileName Gazely.exe
```

The builder uses Windows PowerShell and installed .NET Framework assemblies;
Visual Studio, NuGet and a Python installation are not required.

## One-time speech setup

[INSTALL_FAST_SPEECH_AI.bat](EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/INSTALL_FAST_SPEECH_AI.bat)
downloads the whisper.cpp runtime and tiny.en/base.en models. Existing downloaded
files have been retained. Separate setup launchers support individual models.
Internet is needed for downloads; the current application performs recognition
locally afterward. Model/runtime choices for the production product are not final.

## Diagnostics and maintenance

The application writes `scripture-diagnostics.log` beside the executable,
including transcripts and timing. It rotates at approximately 2 MB. Treat it as
local session data. Live microphone audio is not saved by the application.

The local `version-map.txt` is installation-specific. Recalibrate when Bibles are
added or removed. Reset version tracking after changing a version manually in
EasyWorship. Never copy this church's version indexes as universal defaults.

Gazely assumes KJV is highlighted on first attachment to an EasyWorship process,
using KJV's configured index. Unchanged versions leave the version list untouched;
switches use the index difference. Listening stop/start preserves this tracking.
This startup assumption is not selection readback: if EasyWorship is already on
another version when Gazely starts, use **Reset version tracking** first.
This action is available in Live workspace's Detections options menu and Present.
Clicking **Present** sends the valid reference immediately, without a second
confirmation dialog. Automatic Live remains a separate opt-in mode.

Tests, validation evidence and remaining limitations are documented in the
[engineering handoff](ENGINEERING_HANDOFF.md#validation-and-evidence).
