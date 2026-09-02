# CodeWorker CLI — Epics

The list of epics required to reach a working `verify` command — the CodeWorker CLI's first feature —
derived from [`CodeWorker.Cli/README.md`](CodeWorker.Cli/README.md) (the source of truth for *what* the
tool is). The CLI is a personal toolbox that will grow more commands over time; verification is where it
starts, and these epics cover that first command.

**Workflow** (mirrors the Apostil convention): pick an epic from **MVP** (the next target) or **Full**,
expand it into a detailed epic document, have a **phased plan** produced (one context-isolated phase file
per phase + an overview + an orchestrator), run the orchestration, then flip the epic's line to `- [x]`
when its work is complete.

Epics are deliberately **small** — each is one coherent slice that finishes in a short run of phases
(≈3–6). If an epic can't be broken into a handful of atomic, independently-revertible phases, it's too
big; split it. The work is bucketed by milestone so it reads at a glance:

- **MVP** — the minimum to have a working, *deterministic*, blocking gate that Claude invokes on a
  finished class and that catches the inverted boolean. No LLM yet — the core promise is provable
  without one.
- **Full** — everything else the README describes: the standards depth, the LLM intent judge, the
  language-agnostic seam proven with a second language, and the speed hardening.
- **Done** — shipped, grouped as it was built.

Within each bucket, `###` groups and their order are **dependency order**. Where an epic sits away from
its natural group, an italic note says which dependency put it there. Edit freely.

---

## MVP — a working deterministic blocking gate (next target)

_The smallest end-to-end that delivers the core promise: Claude finishes a class + its tests, calls
`verify`, and a fail-fast pipeline of **deterministic** gates (compile → test → mutation) either passes
or blocks with an exact reason — proving "change any line → a test fails" without spending a single
token. **Hard budget: the deterministic core must run in under 10 seconds** — see the Engine group.
Groups below are in build order._

### Foundation (the pipeline spine)

- [ ] **Verify command surface** — Add a `verify` command to `CodeWorker.Cli` on the existing `IProcessArguments` dispatch: parse the intent file, production file(s), and test file(s) from args; invalid input returns a clear usage error (a value, not an exception). _(Follows the command pattern in `.claude/rules/csharp/naming-and-structure.md`.)_
- [ ] **Intent contract model** — The structured payload Claude submits (`why`, `class`, `responsibility`, `behaviors[]` of `{ behavior, test? }`) parsed and validated into a working-context object the pipeline consumes; a missing test file or unreadable intent is a returned error. _(README: The Intent Contract.)_
- [ ] **Gate pipeline framework** — The `IVerificationGate` abstraction and the fail-fast runner: ordered gates, short-circuit on the first blocking failure, per-gate result (pass/fail, locations, reasons, timing). _(README: How It Works. The spine every gate plugs into.)_
- [ ] **Verdict model + reporting** — Aggregate gate results into one verdict: a machine-readable JSON verdict for the hook and a human-readable report for the morning review. Exit code is 0 on green, non-zero on any block. _(README: Usage.)_
- [ ] **Language engine seam** — Per-language engine resolution behind the gates, with the C# engine (see below) registered; gates never hard-code a language. _(README: C#-first, language-agnostic by design. Proven with a 2nd language later in Full.)_

### Engine (in-process, C#) — the sub-10s foundation

_No per-call `dotnet build` / `dotnet test` / Stryker — that overhead alone blows the budget. Everything
runs in-process on Roslyn against warm reference assemblies. This group is what makes the deterministic
gates fast; the gates below are thin layers over it._

- [ ] **In-process compilation** — Compile the changed class + its test in-memory via Roslyn, resolving reference assemblies once from the project's existing `bin` output (no MSBuild per call). Diagnostics are returned structured. _(The warm workspace every gate sits on.)_
- [ ] **In-process test runner** — Load the compiled test assembly and run only the covering tests in-process (collectible `AssemblyLoadContext`), returning pass/fail per test in milliseconds. _(Depends on: In-process compilation.)_

### Deterministic gates (C#)

- [ ] **Compile gate** — Fail hard on any compiler diagnostic from the in-process compilation, naming the error + location. _(Your rule: not building is failing. Fastest gate, runs first. Depends on: In-process compilation.)_
- [ ] **Test gate** — Fail on any failing covering test, surfacing test names and messages. _(Depends on: In-process test runner.)_
- [ ] **Mutation gate** — Mutate the changed lines by Roslyn syntax-tree rewrite (comparison `>`↔`>=`, boolean negation, `true`↔`false`, arithmetic, return values), re-emit into a fresh collectible `AssemblyLoadContext`, and rerun only the covering tests; **100% of mutants must be killed**, and a survivor is a hard fail naming the exact line and surviving mutation. Equivalent (unkillable) mutants are excluded **only via human-controlled config**, recorded and surfaced — never AI-granted. Coverage folds in — an uncovered line's mutants can't be killed, so it is reported here, no separate coverage gate. _(**The inverted-bool defense — the reason the tool exists.** Depends on: In-process test runner + Scope-to-diff.)_

### Integration & configuration

- [ ] **Tool-level configuration** — A JSON config file (`System.Text.Json`, house style) that **ships with the CLI project** and deploys to the CLI's known install directory alongside the published executable (like `appsettings.json` beside `CodeWorker` today) — **not** per-repo. It drives which gates block vs. warn, human-only equivalent-mutant exclusions, the language engine, and the intent-judge model (default `claude-sonnet-5`). The CLI reads it from its own install location. _(README: Configured From the Outside. A `VerifySettings` class in `Settings/`, mirroring the existing `RepoSettings` loader style.)_
- [ ] **Publish + install to a known directory** — A publish/install step (PowerShell, mirroring `Install-CodeWorker.ps1`) that builds the CLI and deploys it — with its config — to a known local directory Claude invokes. _(The "publish it locally to a known directory Claude will call" step. Depends on: the CLI being runnable end-to-end.)_
- [ ] **Hook wiring + iterate loop** — A Claude Code **`Stop` hook** runs `verify` over the git diff when Claude finishes its turn, pairs each changed class with its test + its **intent file**, and returns `{ "decision": "block", "reason": <verdict> }` on red so Claude iterates — unskippable, because Claude can't end its turn without passing. The loop ends on **green, or a configurable max-attempts** cap on the same unit, after which the verdict is stamped **BLOCKED – needs human** (feeds CodeWorker's existing blocked morning-review outcome). _(Exact `Stop`-hook field names pinned against the installed Claude Code version during this epic. Depends on: Verdict model + Intent contract model.)_
- [ ] **Intent-file workflow rule** — A `CLAUDE.md` instruction that Claude authors an **intent file per class** (the `{ why, class, responsibility, behaviors[] }` contract) as part of finishing a unit of work, so the `Stop` hook has a declared intent to read for every changed class. _(The intent side of the Stop-hook trigger. Depends on: Intent contract model.)_

### Speed (MVP-critical slice)

- [ ] **Scope-to-diff** — The tool derives the diff itself from git (working tree vs `HEAD`) and scopes the mutation gate to the *changed lines*; coverage/mutation never run on the whole solution. A brand-new class diffs as fully new, so nothing is under-tested. _(The single biggest speed lever; a hard prerequisite for the Mutation gate being usable. The AI passes no diff — only the intent contract + file paths. Built into the coverage/mutation gates; called out here so it is tracked.)_

## Full — completes the README

_Everything past the MVP gate that the README describes. Buckets that also appear under MVP hold only
the epics the MVP didn't need._

### Standards gate (Clean Code / SOLID)

- [ ] **Format & analyzer gate** — `dotnet format style` + `dotnet format analyzers` with `--verify-no-changes`, driven by the existing `.editorconfig` / CSharpier config; any diff is a fail. _(Deterministic and cheap; the floor of "following the standards".)_
- [ ] **Structure rules gate** — Enforce the checkable `.claude/rules/csharp` rules on the changed files **deterministically via Roslyn AST checks**: one class per file, file named after the class, no expression-bodied members, records banned, collection expressions, namespace matches folder. Draws on the same shared `.claude/rules` the `standards-review` skill reads — but does **not** invoke that LLM-driven skill (it would blow the sub-10s budget and be non-deterministic). The skill stays a separate human-triggered deeper review. _(See the Reuse open-question resolution.)_

### Intent-match (the one LLM gate)

- [ ] **LLM intent judge** — Judge the code against the declared behaviors: every declared behavior implemented (nothing missing), nothing undeclared (scope-creep / SRP), and every declared behavior backed by a killing test. Defaults to **`claude-sonnet-5` at `low` effort, thinking off, minimal context** (configurable). Runs **last**, only on otherwise-green code; cites code locations for every claim; behind a fakeable seam. Cost is negligible per call — no hard budget, latency is the only cap. _(README: the subjective layer. The deterministic gates remain the primary proof — the LLM is the secondary check. Depends on: all deterministic gates.)_
- [ ] **Spike: intent-call minimization** — Research how far the LLM call can be trimmed so it barely dents the budget: feed the *minimum* context (the class + the declared behaviors, not the whole file tree), suppress extended thinking, force compact structured output, and measure latency vs. judgment quality across a few models. _(A research spike, not a shipped gate — its findings tune the intent judge. Goal: keep the model "thinking less" so the call is fast.)_

### Speed & incremental (hardening)

- [ ] **Build/test caching** — Cache build and test artifacts between iterations of the same unit of work; skip gates whose inputs are unchanged. _(Depends on: the deterministic gates existing.)_
- [ ] **Per-gate timing in the verdict** — Surface where the wall-clock went, per gate, so slow gates are visible and tunable. _(Depends on: Verdict model.)_

### Language-agnostic (deferred — not in scope now)

- [ ] **Second-language engine** _(deferred)_ — Add one non-C# language's in-process (or equally fast) compile/test/mutation engine behind the language seam, proving the core needs no rewrite to extend. **Not being built now — C#-only for the foreseeable.** The **Language engine seam** (MVP Foundation) still exists so this remains a clean drop-in when a real need for a second language appears. _(README: language-agnostic by design remains the design property; the second engine is future work. Sub-10s budget would apply to it too.)_

## Done

_Completed epics, grouped as they were built. Empty — the CLI is a fresh scaffold
(`Program` → `CodeWorkerCliApplication` → `IProcessArguments`); no verification epic has shipped yet._

---

## Open Questions

- ~~**Product name**~~ — **Resolved:** the tool is the **CodeWorker CLI**; verification is its first
  command (`verify`), with more commands to follow. Personal project, not intended for outside use.
- ~~**Intent contract schema**~~ — **Resolved:** fields are `why`, `class`, `responsibility`, and
  `behaviors[]`, where each behavior is `{ behavior, test? }`. Behaviors use **enforced observable
  phrasing** ("Returns X when Y") mapping 1:1 to a verb-first test; the `test` link is **optional** —
  when present the tool checks that test exists and passes deterministically, when omitted the LLM
  infers it. The AI passes no diff; the tool derives it from git to scope mutation to changed lines.
  _(Exact field names finalized in the **Intent contract model** epic.)_
- ~~**Mutation tool**~~ — **Resolved:** **Stryker.NET is rejected** — its out-of-process model
  (project copy + baseline build + full test run, ~20–60s+) can't meet the **hard sub-10s budget**.
  The mutation gate is a **custom in-process Roslyn engine** (in-memory compile, in-process covering-test
  runs, syntax-tree mutation into collectible load contexts). No per-call `dotnet build`/`dotnet test`.
  _(See the **Engine** group and `project_codeworker_cli_verify_speed` memory.)_
- ~~**Mutation threshold policy**~~ — **Resolved:** **100% of mutants on the changed lines must be
  killed.** A survivor blocks and is surfaced. A genuinely equivalent (unkillable) mutant can be excluded
  **only via the external config the human controls** — recorded and surfaced, **never AI-granted** (that
  would be a loophole to wave an inverted bool through). Mutation operators are scoped to the meaningful
  ones (comparison, boolean, arithmetic, return) to keep equivalent mutants rare.
- ~~**Hook mechanism**~~ — **Resolved:** a Claude Code **`Stop` hook** triggers `verify` over the git
  diff and returns `{ decision: "block", reason: <verdict> }` on red (the same decision+reason primitive
  as a `PreToolUse` deny, but at turn-end when the code exists to build/test/mutate — `PreToolUse` is too
  early). Intent comes from a **per-class intent file** Claude authors (a `CLAUDE.md` rule). The loop ends
  on **green or a max-attempts cap**, then stamps **BLOCKED – needs human**. _(Exact `Stop`-hook field
  names pinned during the Hook wiring epic.)_
- ~~**LLM model/tier and per-verification cost budget**~~ — **Resolved:** the intent judge defaults to
  **`claude-sonnet-5` at `low` effort**, thinking off, fed minimal context (the class + declared
  behaviors), configurable via external config to Haiku 4.5 (max speed) or Opus 5 (max rigor). Cost is
  negligible per call, so **no hard budget** — latency is the only real cap.
- ~~**External config format and location**~~ — **Resolved:** JSON via `System.Text.Json` (house
  style). The config is **tool-level, not per-repo**: it ships with the CLI project and deploys to the
  CLI's known local install directory alongside the published executable (like `appsettings.json` beside
  `CodeWorker`). The CLI reads its own co-located config; Claude calls the installed CLI from that known
  directory. A publish/install step (PowerShell, à la `Install-CodeWorker.ps1`) delivers it.
- ~~**Speed target**~~ — **Resolved:** **sub-10 seconds for the deterministic core** (compile → test →
  mutation → standards) per **unit of work = one production class + its paired test file**. The LLM
  intent judge is additive latency on top (a couple of seconds on Sonnet 5 low effort, minimal context)
  and runs only on already-green code, so it never delays a failing verdict; the deterministic-only path
  stays firmly under 10s. _(See `project_codeworker_cli_verify_speed` memory.)_
- ~~**Second language**~~ — **Resolved:** **none for now — C#-only.** The language engine seam stays as a
  proven design property; the second-language engine epic is deferred until a real need appears.
- ~~**Reuse**~~ — **Resolved:** **reuse the `.claude/rules`, not the skills.** The Standards gate
  re-implements the *checkable* rule subset deterministically (Roslyn AST checks + `dotnet format` +
  analyzers), drawing on the same shared rules the skills read — it does **not** shell out to the
  LLM-driven `standards-review` / `unit-test-review` skills (that would blow the sub-10s budget and
  reintroduce non-determinism). `unit-test-review`'s core ("do the right tests exist, can they fail") is
  already delivered deterministically by the **mutation gate**. The skills stay as separate,
  human-triggered *deeper* reviews that complement the fast gate.
