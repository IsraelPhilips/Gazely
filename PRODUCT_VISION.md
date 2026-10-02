# Gazely product vision

Last aligned with the product owner: 30 September 2026.

## Product identity and purpose

**Gazely — Intelligence for live presentation.**

Gazely is the main product: attentive intelligence that understands live context
and helps operators find, prepare and present relevant content in software they
already use. It is not a replacement projection application. Scripture assistance
is the first capability, not the limit of the product or its architecture.

The governing philosophy is Attention → Understanding → Presentation.
Use [Gazely Brand Guidelines](Gazely_Brand_Guidelines.md) as the single source of
truth for brand details. Integration naming is Gazely for EasyWorship, Gazely for
ProPresenter, etc.; integrations do not become separate sub-brands.

## Product commitments

- Build for production reliability: installation, recovery, updates, diagnostics,
  accessibility, maintainability and predictable operation during live events.
- Prioritize fully offline normal operation after one-time setup. No mandatory
  cloud recognition, API key or internet connection in the core runtime.
- Target an ordinary 8 GB RAM Windows PC running Gazely alongside its presentation
  application. A gaming GPU must not be assumed. This is a design target that
  still needs measurement, not a supported-hardware claim already achieved.
- Treat installer size, model downloads, disk footprint, startup time, CPU load
  and sustained memory usage as product requirements. Download only needed assets;
  avoid shipping every model or requiring users to manage development runtimes.
- Aim to match and surpass alternatives in real-event accuracy and responsiveness.
  The user's roughly 98% Pewbeam result is the comparison target, not a measured
  Gazely result. Never promise perfection or disguise uncertainty with percentages.
- Human/operator actions take priority. Mentioning content does not by itself
  authorize presenting it. Finding, preparing and sending live are distinct actions.
- Keep presentation software intact: no internal database writes, binary patches,
  injection or changes to its installation/configuration to make an adapter work.

## Independent extension points

Capabilities and adapters evolve independently.

Capabilities:
1. Explicit scripture references and contextual continuations.
2. Quoted/paraphrased scripture retrieval.
3. Songs and lyrics.
4. Presentations/slides, media and other live-event content or cues.

Adapters, in the requested sequence:
1. EasyWorship 7: preserve and harden the existing tested UI automation.
2. EasyWorship 2009: investigate separately; compatibility is not established.
3. ProPresenter, FreeShow, BibleShow and other platforms: use documented APIs
   where appropriate and available, otherwise separately validated integration.
   Their individual API capabilities have not yet been established in this project.

## Target architecture — not yet implemented as separate components

Audio / other future inputs
→ recognition and context
→ capability-specific understanding
→ structured presentation intent
→ operator / automation policy
→ presentation adapter
→ observed result and operator feedback

The recognition engine must be replaceable without rewriting understanding or
adapters. Scripture parsing/retrieval can be tested without a presentation app;
adapters can be tested with supplied content requests without a microphone.

A presentation intent should carry content type, requested operation (find,
prepare, present), content identity/query, source evidence, interpretation method,
confidence evidence, provisional/final state and a correlation identifier.
Scripture-specific book/chapter/verse/translation data belongs in its typed
payload rather than defining the entire adapter contract.

Adapters declare supported content/actions and report readiness, success,
unsupported operations and failures. Missing capabilities must be visible rather
than silently translated into a different action. Their exact API contracts are
still to be designed after current behavior is protected by tests.

Users enable supported adapters in Settings → Integrations. Gazely manages any
necessary adapter downloads, discovers/connects automatically where possible,
and guides only required setup. Installed local integrations remain usable
offline. Multiple enabled adapters do not imply simultaneous presentation:
the active destination and its verified state must be explicit. Planned adapters
are labeled honestly until implemented and tested.

The complete application must have a modern, refined, cohesive interface rooted
in the brand guidelines, including onboarding, live operation, Settings and
recovery. Interface design begins alongside engine development and is a release
deliverable, not a deferred cosmetic task. The execution plan defines screen,
accessibility, performance and integration-setup acceptance checks.

Keep the literal transcript separate from inferred content. Explicit references,
phonetic suggestions and semantic scripture matches are different evidence types.
A syntactically valid reference is not proof that the preacher actually said it.

## Technology direction

The existing application and EasyWorship integration are C#/.NET Framework.
Retain that working foundation while introducing boundaries incrementally.
Python may support research, evaluation or model preparation; users should not
have to install Python, use a terminal or maintain environments. Any necessary
inference runtime should be packaged and managed by Gazely. A production speech
engine has not been selected. Larger Whisper models and alternative local engines
are benchmark candidates, not committed dependencies.

## Quality and resource evaluation

Use identical real recordings across systems, plus manually checked reference
transcripts. Include Nigerian English voices, normal volume changes and pauses,
quiet speech, music beds, reverberation, corrections, ordinary conversation and
silence. Separate tuning recordings from held-out evaluation recordings.

Track:
- Word substitutions, omissions and insertions (word error rate).
- Exact book/chapter/verse/translation accuracy and missed reference rate.
- Unspoken words/books and false reference detections during non-scripture speech.
- Time from sufficient spoken evidence to first candidate, stable correct result,
  prepared content and verified presentation; report median and tail latency.
- Cold start/model load time, sustained CPU use, peak memory, disk/download size,
  and responsiveness while the presentation software is active.
- Long-session stability, device loss/recovery, stale results, cancellations,
  manual intervention and adapter reconnection.

Numerical acceptance thresholds and download/memory budgets must be agreed from
measurements on representative 8 GB systems. Do not substitute synthetic phrase
success, inference-only timings or parser heuristic scores for those results.

## Delivery sequence

1. Preserve the working prototype and adopt these product/engineering boundaries.
2. Obtain representative sermon recordings and establish reproducible benchmarks.
3. Compare no/neutral prompts, context-preserving audio handling and stronger local
   recognizers on target hardware. Investigate the current unspoken-book output.
4. Select the measured accuracy/latency/resource compromise; stabilize reference
   interpretation and add separately evaluated quote/paraphrase retrieval.
5. Harden Gazely for EasyWorship 7: operator control, failure recovery, packaging,
   diagnostics, accessibility and service-length testing.
6. Implement the adapter contract and validate EasyWorship 2009 independently.
7. Expand presentation adapters and content capabilities according to user value.

Design extension points now without prematurely implementing songs/media or
rewriting the proven controller. Visual branding follows the approved guideline:
calm, precise, predominantly dark/neutral, restrained semantic Signal Lime, text
alongside color states and operator-first behavior. The custom logo must not be
recreated using a substitute font or invented artwork.
