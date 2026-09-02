# CodeWorker CLI

> A personal command-line toolbox for keeping AI-written code honest. Its **first command, `verify`**,
> is an early line of defense that checks AI-written code is doing what it *intended* and following the
> standards you've set — deterministically, while the code is being written. More commands will follow
> over time; verification is where it starts.
>
> **Status:** design. See [`../epics.md`](../epics.md) for the build plan.

---

## The Problem

AI writes plausible code fast. The dangerous failure mode isn't code that *looks* wrong — a reviewer
catches that. It's code that looks right and is subtly wrong:

- an inverted boolean,
- an off-by-one `>=` that should be `>`,
- a test that *executes* a line but never actually *asserts* on its behavior.

Those slip past human code review. This CLI catches them **before they ever reach review** — as an
automated gate that Claude (or any AI) must pass while it writes.

> "I want to ensure that when Claude — or any AI — is writing code, it is doing what it intended
> and following the standards I have set forth."

---

## What It Does

When an AI finishes a **unit of work** — one production class plus its unit test file — it hands the
`verify` command three things:

1. **Why** — the reason this code needs to exist.
2. **Intent** — what this specific class is responsible for, expressed as a list of declared behaviors.
3. **The code + its unit test file.**

The `verify` command runs a **fail-fast pipeline of gates** and returns a **verdict**. If any gate fails, the
AI gets the exact gate, location, and reason, then fixes it and re-verifies — **it cannot proceed
until the verdict is green.**

It answers three questions:

| Question | Answered by |
|---|---|
| **Does the code do what it intended?** | The intent gate (an LLM judge), against the declared behaviors. |
| **Do the tests fully cover it?** — change *any* line and a test fails. | **Mutation testing** (deterministic; coverage folds in). |
| **Does it follow the standards?** — Clean Code + SOLID + the repo rules. | Compile diagnostics, `dotnet format`, analyzers, and the `.claude/rules`. |

---

## How It Works

```
AI finishes a class + its tests
        │
        ▼
  verify --intent intent.json --production Foo.cs --tests FooTests.cs
        │
        ▼
 ┌─────────────────── Gate Pipeline (fail-fast, in-process) ────────────────────────┐
 │ 1. Compile    does it compile?            (not building = fail)                  │
 │ 2. Test       do the covering tests pass?                                        │
 │ 3. Mutation   flip each changed line — does a test die?  ◄─ the inverted-bool    │
 │ 4. Standards  Clean Code / SOLID / .claude rules  (deterministic where possible) │
 │ 5. Intent     does the code do exactly what was declared? (the one LLM step)     │
 └──────────────────────────────────────────────────────────────────────────────────┘
        │
        ▼
  Verdict  →  green: AI proceeds   |   red: gate + location + reason → AI iterates
```

Gates run **cheapest and most objective first**, so it fails fast. A broken compile never pays for a
mutation run, and the single LLM step runs **last** — only on code that already compiles, passes, is
mutation-proven, and is standards-clean.

> **Speed is a hard requirement, not a nicety.** The deterministic core (compile → test → mutation →
> standards) must return a verdict in **under 10 seconds** so it can sit in the write loop. That budget
> is why the engine runs **in-process** — see below.

### Why mutation testing is the heart of it

Coverage alone is a trap. A line can be *covered* (executed by a test) yet *unverified* (no assertion
actually depends on it). Mutation testing closes that gap:

> The mutation gate flips `>=` to `>`, flips `true` to `false`, inverts each `if`, and reruns your
> tests. If nothing goes red, your coverage is a lie — the line ran but was never verified.

That's the inverted boolean a human reviewer misses, caught deterministically. It's why the engine is
**hybrid** — deterministic checks *prove* the objective gates; the LLM only *judges* intent. A killed
mutant is a guarantee; an LLM's opinion is not.

### The engine runs in-process — that's how it stays under 10 seconds

Off-the-shelf mutation testing (e.g. Stryker.NET) is **out-of-process**: it copies the project, runs a
full baseline build, runs the whole test suite, *then* mutates — tens of seconds before it's useful.
That's fine for a nightly CI job and useless for a live gate. So the CodeWorker CLI does none of that.
Instead the C# engine works **in-process on Roslyn**:

- **Compile in memory** — the changed class + its test are compiled with Roslyn against the project's
  already-built reference assemblies. No `dotnet build`, no MSBuild per call.
- **Run only the covering tests in-process** — the test assembly is loaded and the relevant tests run
  directly, in milliseconds.
- **Mutate the changed lines in memory** — each mutation is a syntax-tree rewrite, re-emitted into a
  fresh collectible load context and re-run against only the covering tests.

The result: dozens of mutants exercised in a couple of seconds, not minutes. The one gate that reaches
the network — the LLM intent judge — is being tuned separately to spend as little time as possible (feed
it minimal context so it "thinks less"), and it stays out of the tight deterministic loop.

---

## The Intent Contract

The AI declares intent in a structured, checkable form. This one document is the spec the code is
judged against, the checklist the tests must cover, and the single-responsibility boundary — all at once.

```json
{
  "why": "Tasks that exceed the token limit must be re-queued as pending, not failed.",
  "class": "TokenLimitHeuristic",
  "responsibility": "Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.",
  "behaviors": [
    { "behavior": "Returns TokenLimited when the transcript contains the limit marker",
      "test": "ReturnTokenLimitedWhenMarkerPresent" },
    { "behavior": "Returns null when no marker is present",
      "test": "ReturnNullWhenNoMarkerPresent" }
  ]
}
```

Two rules make the behavior list checkable:

- **Observable phrasing (enforced).** Every behavior reads as an observable input→output outcome —
  "Returns X when Y", "Throws Z when W" — never an implementation detail ("uses a switch"). This maps
  each behavior 1:1 to one verb-first, one-assertion test method, and it's exactly what a surviving
  mutant points back at.
- **Optional test link.** A behavior may name the `test` method that proves it. When named, the tool
  **deterministically** checks that a test by that name exists and passes — no LLM guess. When omitted,
  the intent gate infers the mapping.

The intent gate then checks the code does **exactly** these behaviors — nothing missing, nothing
undeclared (scope creep is an SRP violation) — and that **every declared behavior has a killing test.**

### The AI does not pass a diff

The AI passes only the intent contract and the file paths. The tool reads the **full files** (it needs
them to build, test, and judge intent) and **derives the diff itself** from git (working tree vs `HEAD`)
to scope the mutation gate to the *changed lines* — the biggest speed lever, at zero extra authoring
cost. A brand-new class diffs as fully new, so nothing is ever under-tested.

> Schema is close to final; exact field names are settled in Epic **Intent contract model**.

---

## Usage

> Command surface is being built. This is the intended shape.

Run manually (or from CI) against one class:

```bash
verify --intent intent.json --production Foo.cs --tests FooTests.cs
```

- **Exit code 0** — green verdict, the AI proceeds.
- **Non-zero exit** — a gate failed. The verdict names the gate, the location, and the reason. The AI
  fixes and re-runs.

The `verify` command emits **two outputs**:

- a **machine-readable JSON verdict** the Claude Code hook feeds back to the AI, and
- a **human-readable report** for the person reviewing the work the next morning.

### How Claude actually hits it — a `Stop` hook

In daily use you don't run `verify` by hand. Claude is wired to it through a Claude Code **`Stop` hook**
so the gate is **unskippable** — Claude can't end its turn without passing it:

1. As Claude finishes a class, it writes an **intent file** next to it (a `CLAUDE.md` workflow rule),
   declaring the `{ why, class, responsibility, behaviors[] }` contract.
2. When Claude's turn ends, the **`Stop` hook** runs `verify` over the git diff, pairs each changed class
   with its test + intent file, and inspects the verdict.
3. On **red**, the hook returns `{ "decision": "block", "reason": "<verdict>" }` — Claude Code sends
   Claude back to work with the exact gate, location, and reason. This *is* the iterate loop.
4. The loop ends on **green**, or on a configurable **max-attempts** cap — after which the verdict is
   stamped **BLOCKED – needs human** and surfaced for the morning review.

> This is the same decision-plus-reason primitive as a `PreToolUse` deny, but fired at turn-end, when the
> code actually exists to compile, test, and mutate. `PreToolUse` runs *before* an edit — too early for a
> gate that has to build and run the code.

---

## Configured From the Outside

Behavior is driven by a **JSON configuration file that ships with the CLI** (`System.Text.Json`, the
repo's house style — like `appsettings.json` beside the `CodeWorker` executable). It is **tool-level,
not per-repo**: when the CLI is published to its known local install directory, the config deploys
alongside it, and the CLI reads its own co-located config. Claude invokes the installed CLI from that
known directory. You control the gate without editing code:

- which gates **block** vs. warn,
- **equivalent-mutant exclusions** — the mutation bar is **100% killed on changed lines**; a genuinely
  unkillable mutant can be excluded here, and **only here** (never by the AI), so it's a decision you
  make and can see, not a loophole the AI can grant itself,
- the **language engine** to use,
- the **intent-judge model** — defaults to `claude-sonnet-5` at low effort (thinking off, minimal
  context); switchable to Haiku 4.5 for max speed or Opus 5 for max rigor. Cost per call is negligible,
  so there's no spend cap — latency is the only real constraint.

---

## Design Principles

| Principle | What it means here |
|---|---|
| **Hybrid, not LLM-only** | Deterministic tools prove the objective gates. The LLM judges only intent-match. |
| **Gate a completed unit of work** | Verify a coherent class + its tests, not every keystroke. |
| **Hard block → iterate** | A failed gate stops the AI. It fixes and re-verifies until green. Enforcement, not suggestion. |
| **Fast — a hard sub-10s budget** | The deterministic core runs **in-process on Roslyn** (no per-call `dotnet build`/`dotnet test`/Stryker), scopes mutation to the *changed lines* from the git diff, and fails fast. Minutes-long runs make the tool useless in daily work. |
| **C#-first, language-agnostic by design** | The in-process C# engine ships first; a language-abstraction seam lets other languages plug in without a rewrite — each held to the same speed budget. |

---

## Where This Fits

The CodeWorker CLI lives in `CodeWorker.Cli`, alongside — but separate from — the CodeWorker overnight
task-runner. It is built on the same stack: **.NET 10 / C#** on the **FatCat** toolkit, Autofac DI via
`SystemScope`, Serilog logging, and the command pattern (`args[0]` selects an `ICommand`) — so `verify`
is the first of what will be several commands. It follows every rule in [`../CLAUDE.md`](../CLAUDE.md)
and `.claude/rules` — the tool that enforces the standards is itself held to them.

---

## Roadmap

The work is broken into small, phase-able epics in [`../epics.md`](../epics.md), bucketed by milestone:

**MVP — a working deterministic blocking gate (sub-10s):**
- *Foundation* — verify command, intent contract model, gate pipeline, verdict + reporting, language seam
- *Engine (in-process)* — in-process Roslyn compilation, in-process test runner
- *Deterministic gates* — compile, test, **mutation** (the core promise; coverage folds in)
- *Integration* — external config, hook wiring + iterate loop
- *Speed* — scope-to-diff (git-derived, mutation on changed lines only)

**Full — completes the vision:**
- *Standards* — format & analyzer gate, structure-rules gate
- *Intent-match* — the LLM intent judge + a spike to minimize its call
- *Speed hardening* — caching, per-gate timing
- *Language-agnostic* — a second-language engine that proves the seam
