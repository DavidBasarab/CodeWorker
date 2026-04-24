# Consolidate task folder names — single source of truth

## Goal
Eliminate the three-way duplication of task folder names (`todo`, `done`, `blocked`, `pending`, `failed`, `reference`) between:
- [CodeWorker/Commands/Setup/SetupRepository.cs](CodeWorker/Commands/Setup/SetupRepository.cs) — hardcodes `"tasks"`, `"todo"`, `"done"`, `"blocked"`, `"failed"`, `"pending"` as string literals.
- [CodeWorker/Commands/Run/ValidateRepository.cs](CodeWorker/Commands/Run/ValidateRepository.cs) — hardcodes a subset (missing `"failed"` and `"reference"`).
- [CodeWorker/Settings/TaskSettings.cs](CodeWorker/Settings/TaskSettings.cs) — holds the runtime-configurable defaults.

## Latent bug this fixes
`SetupRepository` creates `failed/` but not `reference/`. `ValidateRepository` checks neither. `TaskSettings` defaults include both. A freshly set-up repo passes validation but crashes at runtime the first time it tries to read the `reference` folder. A repo whose `failed` folder is manually deleted still passes validation. Fix this while consolidating.

## Target design
- Create a new class `RequiredTaskFolders` (or similar name) in `CodeWorker/Commands/Run/` that exposes the canonical list of required folder short-names. Suggested shape:
  ```csharp
  public static class RequiredTaskFolders
  {
      public static readonly IReadOnlyList<string> All = new[]
      {
          "todo", "pending", "done", "blocked", "failed", "reference",
      };
  }
  ```
  Decide whether a `static` constants class or an injectable `IRequiredTaskFolders` is more idiomatic given the rest of the codebase. `static` is acceptable here — these are truly constant values, not configuration.

- `SetupRepository.Setup` iterates `RequiredTaskFolders.All` with a single `foreach` that calls `EnsureDirectory` + writes the `.gitkeep` file. The five sequential `EnsureDirectory` calls and five sequential `WriteAllText` calls collapse to one loop.

- `ValidateRepository.Validate` iterates `RequiredTaskFolders.All` for its directory checks. The explicit per-folder `CheckDirectory` calls collapse to a single loop. Keep the separate file check for `settings.json`.

- `TaskSettings` default values should continue to match `RequiredTaskFolders.All` — no behavior change, but add a comment or reference so future edits keep them in sync (or better, have `TaskSettings` defaults derive from `RequiredTaskFolders`).

## Bonus: extract `IReadEmbeddedResource`
While in `SetupRepository.cs`, also extract the static `ReadEmbeddedResource` helper into an injectable `IReadEmbeddedResource` interface. Today it's a `static` method on a class that takes DI — it can't be faked and the project rule says "if something cannot be faked in a test, it is not properly abstracted." Register normally (auto-scan handles it).

## Tests
- Existing `SetupRepositoryTests` and `ValidateRepositoryTests` must continue to pass after adjusting them for the new shape.
- Add a test proving Setup and Validate use the same folder list (prevents the sync bug from recurring).
- Add tests for `IReadEmbeddedResource` if extracted (may qualify for `[ExcludeFromCodeCoverage]` if it's a direct wrapper over `Assembly.GetManifestResourceStream` with no branching — use judgment per the rules).

## Definition of done
- Folder names appear in exactly one place.
- `SetupRepository.Setup` uses a single foreach for folder creation.
- `ValidateRepository.Validate` uses a single foreach for folder validation.
- Setup creates every folder that Validate checks, and vice versa.
- All tests pass; CSharpier clean.
