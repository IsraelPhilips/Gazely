# Gazely — next execution phase

Created: 30 September 2026. Status: Phase 1 replay infrastructure and first
real-time baseline delivered; user-corrected transcript received and scored.
Phase 2 speech changes are in progress; timestamped labels and held-out evaluation
remain pending.

2 October follow-up: range-first speech presentation, contextual clear navigation,
chapter-only drafts, bounded continuation retrieval and main-card quote updates are
implemented. Both supplied text excerpts have UI regressions (Matthew 6:33 and
Isaiah 51:1–2). Detections now have confirmed Clear and distinct scripture/source
text. Acoustic recognition is unchanged. Next validation must use audio and timed
labels, measure false suggestions per hour and queue latency, and preserve the
current text/UI regression suite. Ordinary anecdotes still produce semantic false
positives; do not enable automatic live output for general meaning matches.

1 October candidate-search delivery: implemented multi-result lexical retrieval,
multi-reference extraction, serial background jobs and a session candidate panel
with search, pagination, pin/dismiss, expanded view and immediate row Present.
Compact BGE + MiniLM offline meaning search is installed with KJV/WEB indexes;
no Qwen or similarly large model was downloaded. Author-written text fixture recall
varies from 6/11 to 14/15 and extra false suggestions remain, so this is an evaluation
increment, not completion of Milestone 4's accuracy gate. See ENGINEERING_HANDOFF.md
for measurements and evidence. Next: timestamped real-sermon quote labels, precision
and recall evaluation including negative speech, sustained queue latency, combined
ASR/search memory on 8 GB, and installer/accessibility work. Preserve review-only
semantic results until those gates are met; do not lower thresholds merely to fill
the panel.

Delivered UI increment: the approved visual direction now covers all seven sections,
with compact rounded inputs, local KJV preview, workspace history and direct history
Prepare/Present actions. Proactive drafts, operator hold/release, canonical validation
and automation switches remain in place. Verified through actual WinForms callbacks,
history eligibility/snapshot tests, engine selector tests and dark/light renders. Full recognition
accuracy, adapter live readback and broader hardware/DPI evaluation remain open.

[First Phase 2 delivery](benchmarks/PHASE2_PROGRESS.md): no-prompt live replay
reduces WER from 45.88% to 40.31% and p95 ready delay from 3.75 s to 1.67 s.
The evaluation build adds repetition protection. Tested neural VAD worsened WER
and remains disabled. Recognition quality/context work is still open.

Follow-up: compressed small is available as an opt-in More accurate evaluation
profile (30.73% live WER, p95 ready 5.44 s). Longer base windows reached 35.19%
WER but delayed the Genesis reference transcript by 5.19 s, so window defaults
remain unchanged. Next priority: preserve recognition context while committing
complete references promptly; do not substitute post-voice delay for semantic
reference latency.

Current execution update: the development build now handles same-chapter ranges,
split range phrases, corrections and contextual verse navigation. The real-time
`phase3-reference-live` replay detects Genesis 49:3–4 and the subsequent “Next
verse” as Genesis 49:4 (52 finals, two detections, zero errors/dropped finals).
Its 40.09% WER confirms that parser progress does not solve recognition quality.
Offline lexical quotation suggestions now use a local 31,102-verse KJV corpus,
carry verse evidence and always require review. Nineteen quotation fixtures and
context-window checks pass; this is not held-out precision/recall validation.
Reprocessing the small-model transcript finds the split Genesis 49:4 quotation;
the base transcript still fails the quotation lookup. Adapter live-state readback,
full interface, stronger speech evaluation and production packaging remain open.

Adapter/UI execution update: a typed adapter contract and cancellation/deduplication
coordinator now wrap EasyWorship 7; 20 fake-adapter lifecycle checks pass.
Integration settings persist enablement and reconnect locally. A new six-page
dark/light Gazely shell has replaced the main legacy layout; compact workspace
and quotation-evidence renders were inspected. Actual live readback, real adapter
actions after the STA change, additional adapter package lifecycles, onboarding,
model management, complete accessibility/DPI checks and release validation remain
open. These changes advance milestones 5–6 without declaring their exit gates met.

Latest UI follow-up adds session suggestion History with explicitly labelled
match-score percentages, an input meter in place of the voice/no-voice badge,
and the newly supplied PNG brand symbol/app icons. Quotation retrieval remains
lexical; semantic recall and recognition errors remain unresolved quality work.

A bounded, review-only pronunciation candidate step now proposes chapter numbers
inside explicit book/chapter/verse calls. The supplied “for tonight” example yields
possible Genesis 49 while its conflicting verses stay unresolved. Nine pronunciation
fixtures and an actual UI callback check pass; broader voices, accents and real-audio
recovery tests remain required before treating this as a general solution.

[Phase 1 evidence](benchmarks/runs/phase1-sermon-realtime/RESULTS.md): 52 final
results, no dropped final windows, zero detections; the correctly transcribed
Genesis plural/range reference was rejected. That baseline predates Phase 2 changes.

## Outcome and scope

Deliver an offline Gazely for EasyWorship 7 evaluation build that accurately
transcribes ordinary sermon speech, identifies explicit and quoted scripture,
follows contextual verse instructions, and responds promptly without inventing
content or overriding the operator. Deliver a complete, beautiful modern Gazely
interface with a calm, classy visual style following the brand guidelines, and
a Settings experience for enabling and connecting supported presentation adapters.

This is the execution backlog. [Product vision](PRODUCT_VISION.md) remains the
authority for product boundaries; [engineering handoff](ENGINEERING_HANDOFF.md)
records shipped behavior. The [sermon benchmark](benchmarks/2026-09-30-sermon/RESULTS.md)
provides the starting evidence. Update these documents rather than creating
additional overlapping plans or stage reports.

Keep C# and the proven EasyWorship controller. Python remains evaluation tooling.
Introduce replaceable recognition, interpretation, action-policy and adapter
boundaries incrementally. Future content types must fit those boundaries, but
songs/media and implementation of additional presentation adapters remain later
deliveries. The adapter framework, integration Settings, installation lifecycle
and full interface design are in scope now; EasyWorship 7 is the first working
integration used to validate them.

## Milestone 1 — repeatable live-pipeline evidence

Deliver a file-replay input that sends decoded audio through the same framing,
segmenter, queue, persistent inference server and detector as live capture.
Provide a real-time pacing mode and an accelerated test mode; distinguish their
timings. Disable presentation actions in replay by default and record intents.

- Preserve the current source/build and baseline outputs for comparison.
- Label the supplied recording with a manually checked transcript, reference
  boundaries, quoted passages and pauses. Keep uncertain words marked uncertain.
- Obtain additional representative recordings for held-out evaluation; continue
  implementation with available material while that collection is pending.
- Include Nigerian English, quiet speech, normal volume changes, music, room
  noise, ordinary conversation, corrections and long continuous speech.
- Add separate verified noise-only samples. Do not assume the supplied recording
  includes the exact microphone noise that caused the reported Nephi repetition.
- Log audio timestamps, queue delay, inference time, provisional/committed text,
  reference decision, preparation and observed presentation separately.
- Preserve actual decoded test segments locally for reproducibility; microphone
  recording requires explicit opt-in and a clear retention/delete setting.

Exit: one reproducible command produces comparable transcripts, reference events,
latency distributions and resource measurements. Each error can be traced to
capture, recognition, interpretation, policy or adapter behavior.

## Milestone 2 — trustworthy speech at normal speaking volume

- Remove the global Bible-name prompt from the baseline. Evaluate terminology
  hints only as controlled alternatives; never replace literal text with guesses.
- Integrate the already downloaded neural VAD with pre-roll, speech padding and
  pause handling. Verify quiet words and final syllables are retained.
- Add evidence-based rejection of noise hallucinations and runaway repetition.
  Do not reject a legitimate repeated word merely because it repeats.
- Preserve continuity across windows. Compare window/overlap settings and use
  stable text reconciliation so overlap does not duplicate or erase speech.
- Ensure partial and final results are revisions of one utterance, with IDs and
  session generations. Keep obsolete work from delaying or overwriting current
  results; measure any speech coverage lost by bounded-queue decisions.
- Compare unprompted base and the measured small Q5 candidate through the live
  pipeline. Test a stronger offline candidate if neither clears the quality gate.
  Select the default from evidence, not model name or download size alone.
- Show provisional and committed text clearly without adding an artificial UI
  handoff delay. Detection can inspect stable partial evidence; repeated revisions
  must not generate repeated actions.

Exit: improved speech and scripture-bearing words on held-out recordings, no
runaway noise repetition in the test suite, complete speech-coverage accounting,
and measured latency/memory compatible with the proposed budgets below.

## Milestone 3 — explicit references, ranges and contextual navigation

Introduce structured results for a reference, a verse range and a navigation
instruction. Carry source text/audio span, utterance ID, evidence type, revision
state and context version. Keep detection independent of EasyWorship automation.

- Handle Genesis one one, spoken numbers, numbered books, punctuation, plural
  verses, same-chapter ranges and references spanning adjacent segments.
- Validate against book/chapter/verse metadata. Ambiguous repairs such as CS3
  remain uncertain; do not silently strip letters to force a reference.
- Implement corrections such as “verse four—sorry, verse five”; replace the
  pending interpretation before action rather than presenting both.
- Track displayed, prepared and spoken/discussed references separately, including
  translation, origin, verification time and manual operator changes.

Context rules and mandatory tests:

| Situation / phrase | Expected behavior |
|---|---|
| Verified live Genesis 49:3; “next verse” | Resolve Genesis 49:4 |
| Same context; “verse four” | Resolve Genesis 49:4 |
| Same context; “verses four to six” | Preserve Genesis 49:4–6 as a range |
| Same context; “previous verse” / “go back one verse” | Resolve Genesis 49:2 |
| Standalone “next” | Require scripture-navigation evidence; otherwise abstain |
| “Next week we will…” | No navigation |
| New explicit book/chapter followed by “verse four” | Use the new spoken context |
| Displayed and spoken context conflict without a clear cue | Show ambiguity; no automatic action |
| Operator changes live passage manually | Refresh verified live context; invalidate stale pending action |
| Live slide is non-scripture / readback unavailable | Do not assume old displayed scripture is still authoritative |
| Same “next verse” repeats across partial/final revisions | One intent, at most one action |
| Two genuinely separate “next verse” instructions | Two advances from updated context, when policy permits |
| Chapter or book boundary | Validate canonical successor/predecessor; make transition explicit |
| No usable context / invalid verse | No fabricated reference or automatic presentation |

Context lifetime depends on continuity and evidence: a verified live passage may
remain relevant through a long explanation, while an unverified old mention must
not silently control navigation. Do not use a single timeout for all context.

Exit: deterministic parser/state-machine cases pass, including the recording's
Genesis 49:3 → “next verse” → 49:4 sequence. The adapter preserves range semantics;
unsupported operations are visible rather than silently reduced to one verse.

## Milestone 4 — offline quotation and paraphrase retrieval

### Next execution sequence — 1 October 2026

Keep this work separate from speech transcription: display the words actually
recognised, and show inferred passages as suggestions with evidence. Default new
references to KJV unless the preacher explicitly requests another translation;
an operator changing EasyWorship's selection must not change this policy.

1. **Measure the existing misses before tuning.** Replay the corrected sermon
   transcript separately from actual ASR output. Label Genesis 49:3–4, the
   subsequent quotation of 49:4, Matthew 6:33, and ordinary speech/noise. Mark the
   earliest point with enough distinctive quoted words. Record candidate scores,
   rejected candidates, rejection reasons and lookup-to-display timing. Separate
   recognition errors, retrieval misses and suggestions discarded by UI scheduling.
2. **Preserve quotation context and completed work.** Replace the three-final-chunk
   limit with a bounded, timestamped word window and overlapping shorter windows.
   Start with a 30-second/120-word cap and measure alternatives; these are trial
   limits, not validated settings. Queue at most one active lookup and one newest
   pending snapshot backed by a transcript ledger: coalescing work must preserve
   every unprocessed phrase, including a burst containing several quotations.
   Give results evidence timestamps/IDs so newer speech does not
   automatically erase a valid earlier match. Session changes still cancel work;
   stale passages must not replace a newer explicit reference or operator-held edit.
   Accepted earlier suggestions can enter history without taking over the main card.
3. **Improve exact and near quotations first.** Retrieve distinctive phrases and
   adjacent-verse spans, then rank by rare-word coverage, word order and supporting
   phrase length. Split compound speech into overlapping clauses and short windows;
   search each independently, not only one embedding of the whole sermon paragraph.
   Add conservative inflection/archaic-word equivalence for retrieval
   only. Evaluate bounded tolerance of missing/misheard words without rewriting the
   transcript or mapping arbitrary words into Bible terms. Use the recent explicit
   passage as a ranking boost, while retaining whole-Bible search for topic changes.
4. **Present useful evidence and ambiguity.** Show the heard phrase, corresponding
   verse words and match strength. Initially expand three alternatives for a single
   ambiguous phrase, with More available; this is NOT a cap on distinct passages
   found across speech. Retain all qualifying suggestions in the session panel.
   Otherwise abstain. Consolidate repeats in
   history. Keep quotation suggestions operator-reviewed and separate from explicit
   reference confidence; match percentages are not calibrated certainty.
5. **Evaluate paraphrases as a separate offline experiment.** Only after lexical
   results are measured, compare a small quantized embedding model and optional
   reranker against that baseline. Keep it optional until accuracy, added download,
   startup time, CPU and memory measurements justify it on an 8 GB machine running
   presentation software. Do not choose a model or promise resource figures without
   measurements. Verify redistribution rights for any additional translation corpus.
6. **Release against held-out speech.** Include multiple speakers, accents, pauses,
   partial quotes, cross-chunk quotes, adjacent verses, paraphrases, repeated verses,
   unrelated religious language, background music and silence. Score clean-text
   retrieval separately from end-to-end audio. Target the existing >=95% quotation
   precision, >=90% recall and p95 <=3 seconds from sufficient evidence; publish
   false suggestions per hour and resource measurements as well. These are release
   targets, not current measured performance. No quotation-driven automatic live
   output in this phase.

Current implementation to replace or extend: ScriptureQuoteIndex.Find uses KJV
token overlap, order, score >=0.88 and a 0.025 top-candidate margin. QuotationContext
retains three final chunks and resets after a 15-second gap. FindQuotation runs on
final text and drops results when interpretationRevision changes. These restrictions
can explain misses; quantify each before lowering thresholds. Partial speech may
support a provisional candidate later, but must never trigger automatic live output.

### Operator candidate panel — agreed next-phase scope

The Live workspace must have a persistent, scrollable **Scripture candidates**
panel with direct row actions. The main suggested-scripture card remains a detail
view; it must not be the only storage for detections. Use the same underlying
session store for this panel and History, rather than two independent copies.

UI/UX acceptance applies to every new panel, button and control: extend the existing
Gazely brand, spacing, typography, rounded surfaces and light/dark themes. Prototype
the layout before implementation; avoid simply adding more boxes to the workspace.
Make reference and Present action immediately scannable, with secondary evidence
progressively disclosed. Check keyboard access, visible focus, contrast, empty and
loading states, long passages, ten-plus candidates, normal/maximized windows and
125%/150% DPI. Preserve transcript visibility and compact input widths. Review
rendered screens and operator click paths before declaring the UI complete.

- Retain distinct quotes, paraphrases and uncertain explicit references. Ten
  separate passages in a short burst must remain selectable; do not replace nine
  with the most recent one or impose the existing latest-12 rendering limit as
  data retention. Virtualize rendering as the session grows.
- Each row shows canonical reference/range, short KJV excerpt, heard phrase,
  timestamp, evidence type and match-strength label, plus **Present** beside it.
  Offer Prepare, pin, dismiss and expanded evidence as secondary actions.
  Present is one click with no confirmation dialog for a valid complete candidate.
  Incomplete book/chapter mentions remain visible but offer Complete/edit rather
  than a misleading enabled Present button. Valid uncertain alternatives are
  selectable manually; uncertainty does not authorize automatic presentation.
- Group alternatives from the same heard phrase. Different phrases can yield
  multiple independently relevant passages, including overlapping verse ranges.
  Consolidate repeated reference/range occurrences while preserving evidence and
  occurrence timestamps. Keep a separate presentation-attempt history.
- Freeze row identity/order while the pointer is over actions or keyboard focus
  is inside the list; incoming results must never swap the target under a click.
  Capture an immutable candidate ID/reference/version snapshot on activation.
  New arrivals do not steal focus or scroll the operator away; show a new-items
  indicator when they are browsing older candidates. Retain pinned rows.
- States include provisional, supported, weak/possible, superseded/retracted,
  dismissed and sent-unverified. Corrections to partial ASR update the same evidence
  span rather than accumulating invented verses. Superseded hypotheses remain
  inspectable but are not silently offered as current verified suggestions.
- Separate detection score from ASR quality. Do not label raw cosine similarity
  or reranker logits as a percent probability. Calibrate numeric confidence on
  held-out labels before displaying it as certainty; until then use match strength.
- New references default to KJV absent an explicit spoken translation request.
  A retrieval match against another translation does not change output version.
  Operator actions produce typed presentation intents through the adapter layer.

### Hybrid retrieval and model selection

Pipeline: timestamped transcript ledger -> overlapping clauses/windows -> explicit
reference parser + lexical retrieval + semantic retrieval -> merged candidates ->
bounded reranking -> evidence/ambiguity policy -> session candidate store -> panel.
Search individual verses and adjacent-verse windows, with canonical mappings back
to verse IDs. Precompute corpus embeddings at packaging time; only incoming speech
needs embedding at runtime. Compare exact vector scans before adding an approximate
index: the corpus is small enough to benchmark both without assuming a vector server.

Initial benchmark candidates, not selected production models:

| Role | First candidate | Comparison / decision |
| --- | --- | --- |
| CPU semantic retriever | BAAI/bge-small-en-v1.5 | Compare intfloat/e5-small-v2 on sermon paraphrases and ASR errors |
| Shortlist reranker | cross-encoder/ms-marco-MiniLM-L6-v2 | Compare a smaller TinyBERT reranker and no reranker; evaluate top 10–20 merged candidates initially |
| Higher-capacity experiment | Qwen/Qwen3-Embedding-0.6B | Optional only if measured gains justify CPU, RAM and download costs with ASR running |
| Runtime | C# with ONNX Runtime for supported compact exports | Validate tokenization, pooling, query instructions and quantized-output parity; no end-user Python dependency |

Rerank per clause, retaining multiple evidence-supported matches rather than forcing
one winner. Use rank fusion for lexical/semantic lists before calibrated reranking;
do not average incompatible raw scores. Context (recent explicit book/chapter,
names, events and distinctive claims) is a boost, not a hard filter. A broad phrase
such as "God will help you" must not become a confidently identified quotation.
Maintain a weak/possible group for useful ambiguity, without dumping every nearest
neighbour from the search engine into the operator's panel.

For severe paraphrases, evaluate modern-language licensed corpora and curated
paraphrases mapped to canonical IDs. Preserve provenance: generated descriptions
are retrieval aids, never Bible text. If baseline models fail, fine-tune with
human-verified sermon-to-passage pairs and hard negatives sharing the same topic
but different meaning. Split evaluation by speaker/sermon and paraphrase family;
keep an additional unseen-passage set so synthetic training examples cannot leak
into the reported accuracy. A local generative query rewriter is an optional later
experiment, not a required always-running LLM; every result must resolve to actual
indexed passages. No system can uniquely recover an arbitrarily vague paraphrase.

Primary references checked 1 October 2026:
- [Retrieve and rerank architecture](https://www.sbert.net/examples/sentence_transformer/applications/retrieve_rerank/README.html)
- [BGE small model card](https://huggingface.co/BAAI/bge-small-en-v1.5)
- [E5 small model card](https://huggingface.co/intfloat/e5-small-v2)
- [MiniLM reranker](https://huggingface.co/cross-encoder/ms-marco-MiniLM-L6-v2)
- [Qwen embedding research](https://arxiv.org/abs/2506.05176)
- [ONNX Runtime C#](https://onnxruntime.ai/docs/get-started/with-csharp.html)

### Delivery order and acceptance

A. Ship the candidate-store/panel foundation using existing parser and quotation
outputs; add uncertain-reference rows, direct Present, stable selection and history
integration. Replace the single-result retrieval interface with a candidate list.
B. Fix context/scheduling and add lexical multi-passage retrieval; validate bursts
of 2, 3 and 10 references, quoted passages spanning chunks and transcript revisions.
C. Compare semantic models and reranking offline against the same frozen dataset;
integrate the smallest configuration meeting quality and resource targets.
D. Add paraphrase training/corpus expansion only where the measured error taxonomy
justifies it; then run held-out audio with ASR and EasyWorship sharing the machine.

Measure candidate recall@5/10 per evidence span, supported-panel precision, distinct
passage recall for multi-quote bursts, false suggestions/hour, severe-paraphrase
recall, and time-to-first-useful-candidate. Score literal quotes and paraphrases
separately. The existing >=95% precision / >=90% recall goals apply to supported
quotations, not to every exploratory weak alternative. Initial retrieval-stage
p95 target is <=1 second after usable transcript; end-to-end quotation target remains
<=3 seconds from sufficient spoken evidence. Report actual hardware, peak combined
RAM, CPU load, ASR queue growth, cold start and installed bytes. These targets are
not yet proven. UI acceptance includes ten retained candidates, no click-target
movement, immediate manual Present, and zero auto-live actions for weak quotations.

- Select a Bible corpus with verified redistribution rights and stable verse IDs.
- Build local phrase/lexical retrieval first; evaluate semantic retrieval and
  reranking where they improve held-out accuracy within the resource budget.
- Use overlapping context and explicit-reference continuity, while preserving
  the difference between spoken words and inferred scripture.
- Show supporting words for suggestions. Distinguish explicit reference,
  contextual navigation, quotation and looser paraphrase evidence in the UI.
- Evaluate Matthew 6:33 and Genesis 49:4 quotations, plus unrelated speech with
  similar vocabulary. Do not treat Pewbeam's candidate cards as ground truth.
- Deduplicate repeated suggestions and abstain when evidence is weak. Keep
  semantic matches operator-reviewed initially; similarity is not a calibrated
  probability and must not inherit the current parser's automatic-live threshold.

Exit: independently scored quotation precision/recall and false-positive results;
supported suggestions appear with source evidence, without unrelated-card flooding.

## Milestone 5 — adapter framework and verified EasyWorship integration

- Define a small adapter contract for readiness, capabilities, observed state,
  prepare/present actions and result verification. Keep scripture data in a typed
  payload so future song/media capabilities do not require rewriting policy.
- Wrap the tested EasyWorship 7 automation; preserve its established control
  discovery and version mapping. Profile waits before reducing them.
- Track intent IDs/context versions and acknowledge verified results. A retry
  must not advance twice. Cancel superseded/stale work before presentation.
- Manual intervention and Stop take priority. Require current, verified context
  and armed automation policy for relative live actions. Uncertain readback
  disables automatic contextual action while preserving manual assistance.
- Keep preview, prepare and live distinct. Default Automatic Live off. Use
  evidence-specific policy; never globally lower confidence to hide missed matches.

### Settings → Integrations

The intended experience is: tick a supported integration, let Gazely acquire any
needed adapter files, discover/connect to the presentation software, and show
verified readiness. ProPresenter is an example of this future user flow, not a
claim that a ProPresenter adapter currently exists or supports a particular API.

- Maintain an integration catalog with stable adapter IDs, supported application
  versions, capabilities, package versions and dependency requirements. Share the
  intelligence engine and speech models; do not duplicate them per adapter.
- Distinguish Available, Not installed, Downloading, Needs setup, Connecting,
  Ready, Disconnected and Error. Planned integrations are clearly labeled and
  cannot be enabled as though operational.
- Selecting an available integration initiates setup: use bundled files when
  present, otherwise download only necessary compatible dependencies. Show size,
  progress, cancellation and retry; verify integrity before atomic activation.
  Preserve the last working version for rollback. No surprise mid-service updates.
- Auto-discover the local application or supported endpoint where possible.
  Request only genuinely required host, port, authentication or application-side
  enablement, with an optional connection test and clear recovery instructions.
  Store credentials securely and exclude them from diagnostic logs.
- Persist enabled integrations and reconnect on launch or after recoverable
  failure. Unticking disconnects and cancels that adapter's queued actions;
  removing downloaded files is a separate explicit operation.
- Multiple adapters may be enabled, but the active presentation destination is
  explicit. Enabling or reconnecting an adapter never arms Automatic Live or
  broadcasts commands to every enabled application. Keep displayed state and
  reference context separate per destination. Multi-output routing is later work.
- Installed local integrations work without an internet connection. First-time
  downloads need connectivity; show this requirement clearly. Internet or local
  network requirements of each target application are disclosed per adapter.
- Support package installation, connection and lifecycle tests with a fake
  adapter now, then real EasyWorship 7 checks. Add each later adapter only after
  separately verifying its API/automation, licensing and compatibility.

Exit: real EasyWorship checks cover manual changes, translations, ranges, repeated
commands, app focus, disconnection/recovery and cancellation. No internal files
of the presentation application are modified. Integration Settings cover fresh
setup, interrupted download, offline restart, incompatible versions, reconnect,
disable and switching the active destination without stale or duplicate actions.

## Milestone 6 — complete Gazely interface and interaction design

Treat interface quality as a dedicated deliverable across the entire application.
The [brand guidelines](Gazely_Brand_Guidelines.md), especially sections 6–10 and
16, govern the design. The goal is modern, refined, spacious and consistent,
while remaining practical for an operator during a live service.

- Establish reusable typography, spacing, color, icon, control and state tokens.
  Use Gazely Black (#111310), Porcelain (#F3F1EA), restrained semantic Signal Lime
  (#B8F34A) and supporting neutrals. Design dark mode as the primary booth
  experience, with a coherent light alternative. Use Geist Sans or Inter as
  supporting typography subject to packaging/licensing checks.
- Use strong alignment, clear hierarchy, subtle separators, balanced rounded
  surfaces and generous space. Avoid generic AI gradients, excessive glass,
  decorative neon and visually competing controls. Use approved logo artwork;
  do not recreate the custom wordmark with a font.
- Design the full screen set: onboarding and audio check, model download/setup,
  main listening workspace, scripture suggestions and evidence, prepared content,
  verified live passage, contextual navigation, history, Settings, Integrations,
  diagnostics, updates and recovery. Keep advanced tuning out of the main flow.
- Make the main workspace immediately answer: what is being heard, what Gazely
  understood, what is prepared, what is actually live, and which application is
  connected. Keep Start/Stop, preview/present and automation state unmistakable.
  Do not present heuristic confidence as a calibrated probability.
- Design empty, listening, uncertain, ready, downloading, disconnected and error
  states as carefully as the ideal state. Updates must not jump the operator's
  focus or move the action they are about to click.
- Deliver clickable screen prototypes early, including integration selection
  through verified connection and scripture detection through presentation.
  Review real workflows before implementing the complete visual system.
- Evaluate whether the current UI toolkit can meet the design and accessibility
  requirements. If another C# desktop UI layer is justified, isolate the change
  from the working engine/adapter and measure startup, memory and deployment cost.
  Do not choose a heavyweight shell solely for its visual defaults.
- Implement keyboard navigation, visible focus, accessible control names,
  verified contrast, text alongside status colors, reduced motion, high-DPI
  scaling and resizable layouts. Check at 1366×768 and 100/125/150/200% scaling,
  adapting layout so essential controls remain reachable.
- Use restrained brand motion; display results and accept actions immediately.
  Animations must never delay recognition, interaction or presentation.

Exit: the complete interface is implemented and visually reviewed against the
brand, with keyboard/scaling checks and end-to-end operator walkthroughs. No
unbranded legacy dialogs or unstyled setup/error screens remain in normal flows.
Measure the complete application's footprint, including the UI, on target hardware.

## Milestone 7 — target-machine validation and evaluation release

Package the chosen runtime/model with integrity checks, clear download sizes,
offline startup and actionable recovery. No user-managed Python or terminal.
Check licensing, runtime/setup version consistency and missing/corrupt model
handling. Preserve the last working build and document rollback.

Run service-length sessions alongside EasyWorship on an actual CPU-only 8 GB PC,
including microphone loss, silence/music, pause/resume, sleep/recovery and manual
operation. A pass on the current 16 GB i7 laptop is insufficient.

Deliver the evaluation build, concise launch/test instructions, measured results,
known limitations and remaining production blockers. Do not label it production
ready solely because this one sermon passes.

## Proposed acceptance targets

These are initial engineering targets, not achieved results or contractual
hardware claims. Milestone 1 establishes baselines; document any target revision
with evidence instead of silently lowering it. Report sample counts and errors.

| Dimension | Initial target / decision rule |
|---|---|
| Overall transcription | Aim for <=2% normalized WER on manually corrected held-out sermons; publish per-speaker results and scripture-word errors separately |
| Explicit references and contextual navigation | >=99% precision and >=95% recall across the labeled evaluation set; all deterministic boundary/correction cases pass |
| Quotation suggestions | >=95% precision and >=90% recall on labeled quotations; paraphrases scored separately |
| Incorrect automatic presentation | Zero in the release suite; report exposure duration and event count, not a universal guarantee |
| Noise behavior | Zero invented scripture actions and zero runaway repeated output in the labeled noise suite; report speech insertions and false suggestions per hour |
| Stable explicit detection latency | Median <=1 s; p95 <=2 s from the last word needed to identify the intended reference, including endpoint and queue time |
| Quotation detection latency | Initial p95 <=3 s from sufficient quoted evidence; annotate that point consistently |
| Preparation / live latency | Measure separately through verified adapter completion; set budget after profiling the established EasyWorship path |
| Recognition + retrieval memory | Initial combined peak budget <=1.5 GiB; measure total system pressure and responsiveness alongside EasyWorship on 8 GB |
| Download footprint | Initial combined runtime/model/index target <=750 MB; publish measured installer and installed sizes separately |
| Sustained operation | At least one 2-hour service replay on target hardware without unbounded queue/memory growth, crash or duplicate live action |
| Interface quality | Brand-consistent full screen set; readable keyboard-accessible workflows, reduced motion and tested small-screen/high-DPI layouts |
| Integration setup | Working adapter can be enabled, installed if needed, connected, disabled and reconnected without user-managed files; clear active destination and honest readiness |

WER is an evaluation metric, not the same thing as the user's informal accuracy
estimate. Recognition, retrieval and navigation scores must remain separate.
Do not average away a missed reference or unsafe action with correct ordinary words.

## Execution order and first delivery

Execute the engineering milestones 1 → 2 → 3 → 4 → 5, then complete interface
integration (6) and release validation (7). Begin milestone 6's design system and
clickable workflow prototypes alongside milestones 1–2; interface design must not
wait until the engine is finished. Introduce minimal adapter state observation
during milestone 3 wherever contextual tests require it. Milestone 3's pure parser
work need not wait for final model selection; it can use supplied text fixtures.

The first implementation delivery combines the replay harness, removal of the
harmful prompt, measured VAD/continuity improvements, and a comparison report.
The next delivery adds structured ranges and contextual navigation. Quotation
retrieval and integration hardening follow on that tested foundation. Integrate
the new interface progressively, with the full experience required before the
evaluation release rather than deferred to a future cosmetic pass.

Maintain this checklist as work lands:

- [x] Live-pipeline replay tooling and first real-time baseline
- [x] User-corrected whole-recording transcript and baseline WER
- [ ] Timestamped reference/navigation labels and held-out recordings
- [ ] Speech quality, noise rejection and latency improvements
- [ ] Explicit references, ranges and contextual navigation
- [ ] Offline quote retrieval with evidence and abstention
- [ ] Adapter framework, integration Settings and verified EasyWorship actions
- [ ] Brand-led design system and clickable full-workflow prototypes
- [ ] Complete modern interface, accessibility and visual verification
- [ ] 8 GB validation, packaged evaluation build and release instructions
