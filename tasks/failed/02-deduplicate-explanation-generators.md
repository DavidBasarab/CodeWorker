# Deduplicate GenerateFailedExplanation and GenerateBlockedExplanation

## Goal
Eliminate the heavy duplication between [CodeWorker/Commands/Run/GenerateFailedExplanation.cs](CodeWorker/Commands/Run/GenerateFailedExplanation.cs) and [CodeWorker/Commands/Run/GenerateBlockedExplanation.cs](CodeWorker/Commands/Run/GenerateBlockedExplanation.cs). Both classes build a near-identical markdown template with `+` string concatenation and both use `if (text.Contains(...))` heuristic chains for "recommended fix" logic.

## Problems to fix
1. **String concatenation with `+`** is banned by project rules — must be string interpolation or a single multiline template.
2. **Heuristic chains** (`if text.Contains("token limit") ... if text.Contains("authentication") ...`) are polymorphism waiting to happen — each heuristic is a rule with a keyword set and a recommendation string.
3. **Duplicate markdown template** — ~70% of the two files is the same "Timestamp / Exit Code / Claude Output / Error Output / Recommended Fix" block, differing only in title and one extra "Failure Mode" section on the failed variant.

## Target design

### New shared infrastructure
- `IBuildExplanationMarkdown` / `BuildExplanationMarkdown` — builds the common markdown given title, timestamp, exit code, output lines, error lines, optional failure mode, and recommended fix. Interpolation-based, no `+` concatenation.
- `IFixHeuristic` — interface with `bool Matches(ProcessResult result)` and `string Recommendation { get; }`.
- Per-heuristic classes (one per file):
  - Failed: `TimedOutHeuristic`, `FailedToStartHeuristic`, `TokenLimitHeuristic`, `AuthenticationHeuristic`, `DefaultFailedHeuristic`.
  - Blocked: `MissingResourceHeuristic`, `AmbiguityHeuristic`, `DefaultBlockedHeuristic`.
- `IRecommendFix` with two implementations:
  - `RecommendFixForFailedTask` — injects the failed heuristic set (in priority order), returns the first matching recommendation.
  - `RecommendFixForBlockedTask` — same pattern for the blocked heuristic set.

### Refactored generators
- `GenerateFailedExplanation` depends on `IBuildExplanationMarkdown` + `IRecommendFix` (the failed variant — see DI note below). Shrinks to ~15 lines.
- `GenerateBlockedExplanation` does the same with the blocked variant.

### Dependency injection note
Two implementations of `IRecommendFix` exist, so they must be distinguished. Prefer keyed registration or inject the concrete type (`RecommendFixForFailedTask`, `RecommendFixForBlockedTask`) directly — the Autofac auto-scan registers types `AsSelf()` already. Follow the existing module pattern in [CodeWorker/CodeWorkerModule.cs](CodeWorker/CodeWorkerModule.cs) — only add manual registrations when required by the project rules.

## Tests
- Existing tests in `GenerateFailedExplanationTests.cs` and `GenerateBlockedExplanationTests.cs` must continue to pass — the refactor preserves the observable markdown output.
- Write new tests for each `IFixHeuristic` implementation (one test = one match/no-match behavior).
- Write tests for `BuildExplanationMarkdown` (one test per markdown section — Timestamp, Exit Code, Output, Error, Recommended Fix, optional Failure Mode).
- Write tests for each `IRecommendFix` implementation verifying heuristic priority.

## Definition of done
- Zero `+` string concatenation in either generator file.
- Zero `if text.Contains(...)` heuristic chains — replaced by polymorphic heuristic classes.
- Markdown template defined in exactly one place.
- All tests pass; `dotnet build` clean; CSharpier satisfied.
