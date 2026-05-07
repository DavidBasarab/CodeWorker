executes tasks **unattended overnight**. There is no human in the loop between "task lands in `todo/`" and "commit lands in git the next morning." That makes the task file itself the entire contract with an AI that has no memory of prior tasks and cannot ask clarifying questions.

A precise, well-bounded task that took 20 minutes to plan produces a clean reviewable commit. A vague task written in 30 seconds produces noise that takes longer to triage in the morning than it would have taken to plan correctly. **Planning is the work, not overhead.** The new README and the sample template must both lead with this.

## Scope

### Files to add
- `CodeWorker/Commands/Setup/SampleTaskTemplate.md` — new embedded resource. The actual sample task file content (see **Sample Template Content** below).

### Files to change
- [CodeWorker/Commands/Setup/TasksReadme.md](CodeWorker/Commands/Setup/TasksReadme.md) — rewrite (see **README Content** below).
- [CodeWorker/CodeWorker.csproj](CodeWorker/CodeWorker.csproj) — add `<EmbeddedResource Include="Commands\Setup\SampleTaskTemplate.md" />` next to the existing two embedded resources.
- [CodeWorker/Commands/Setup/SetupRepository.cs](CodeWorker/Commands/Setup/SetupRepository.cs) — add one `WriteAllText` call for `sample-task-template.md` after the existing `README.md` write.
- [CodeWorker.Tests/Commands/Setup/SetupRepositoryTests.cs](CodeWorker.Tests/Commands/Setup/SetupRepositoryTests.cs) — add tests for the new file write and for the new embedded-resource read; update the existing README assertion if the assertion string changes.

### Out of scope
- [CodeWorker/Commands/RequiredTaskFolders.cs](CodeWorker/Commands/RequiredTaskFolders.cs) — folder list is correct; do not touch.
- [CodeWorker/Commands/Setup/defaultSettings.json](CodeWorker/Commands/Setup/defaultSettings.json) — settings layout is correct; do not touch.
- [CodeWorker/Commands/Setup/ReadEmbeddedResource.cs](CodeWorker/Commands/Setup/ReadEmbeddedResource.cs) — already generic enough; do not touch.
- [tasks/README.md](tasks/README.md) at the root of this repo — will be regenerated next time `setup` runs against this repo. Do not hand-edit it as part of this task; let `setup` produce it from the new embedded resource on its next run.
- The project-root [README.md](README.md) — separate concern; not part of this task.

## Design

### One new resource, one new write call

The setup command already follows a clear pattern: every embedded resource is read by name via `IReadEmbeddedResource.Read("…")` and written to a fixed path under `tasks/` via `IFileSystemTools.WriteAllText`. Reuse that exact pattern — no new abstractions, no new helpers, no factory.

`SetupRepository.Setup` already does:

```csharp
await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "README.md"), readEmbeddedResource.Read("TasksReadme.md"));
await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "settings.json"), readEmbeddedResource.Read("defaultSettings.json"));
```

Add a third line in the same shape, immediately after the README write:

```csharp
await fileSystemTools.WriteAllText(
    Path.Combine(tasksPath, "sample-task-template.md"),
    readEmbeddedResource.Read("SampleTaskTemplate.md")
);
```

That is the only production code change. Order matters only for test assertion clarity — keep the new write directly after the README write so anyone reading the file sees the two human-facing markdown files together.

### Embedded resource registration

The csproj already has:

```xml
<ItemGroup>
    <EmbeddedResource Include="Commands\Setup\TasksReadme.md" />
    <EmbeddedResource Include="Commands\Setup\defaultSettings.json" />
</ItemGroup>
```

Add the new line in the same group:

```xml
<EmbeddedResource Include="Commands\Setup\SampleTaskTemplate.md" />
```

`ReadEmbeddedResource.Read("SampleTaskTemplate.md")` will then resolve via the existing `FatCat.CodeWorker.Commands.Setup.{resourceName}` prefix without any code change.

### Filename of the dropped sample

The dropped file is named `sample-task-template.md` — kebab-case, no number prefix. **No number prefix is intentional**: the runner processes `tasks/todo/` files in filename order, but this file lives directly under `tasks/`, not inside `tasks/todo/`. It is a reference that sits next to `README.md`. Users copy it into `tasks/todo/` and rename it with the correct numeric prefix (one higher than the highest number in `tasks/done/`) when they are ready to queue a real task. The README must spell this out.

### README Content

Rewrite [CodeWorker/Commands/Setup/TasksReadme.md](CodeWorker/Commands/Setup/TasksReadme.md) so it covers:

1. **What this folder is** — managed by CodeWorker, the overnight task runner for Claude Code (one short paragraph, link the project repo).
2. **Why planning matters** — the "no human in the loop overnight" framing from the **Why Planning Matters** section above. This is the most important new content. Lead with it. Make it loud.
3. **Folder layout** — a table of all seven folders with one-line purposes:
   - `todo/` — queue of task files waiting to run, executed in filename order
   - `pending/` — task currently being processed by the runner
   - `done/` — completed tasks
   - `blocked/` — tasks the runner could not safely complete; each has an explanation file
   - `failed/` — tasks that errored out during execution
   - `reference/` — supporting context the runner can read while executing tasks
   - `logs/` — per-task log output (`<task>.log` and `<task>.live.log`)
4. **Naming convention** — task files use a zero-padded numeric prefix that is **one higher than the highest number currently in `tasks/done/`** (and any other queued task in `tasks/todo/`). Filename order is execution order.
5. **Sample template** — point to the sibling `sample-task-template.md`. Tell users to copy it into `tasks/todo/`, rename it with the next number, and fill it in.
6. **Where to learn more** — link to the project README's "Task File Requirements" section.

Keep it tight — README, not a manual. Match the tone of the existing `TasksReadme.md` (terse, link-driven). Use Markdown tables for the folder layout.

### Sample Template Content

The body of `SampleTaskTemplate.md` should mirror the **Recommended Task Template** in the project [README.md](README.md) (lines 396–425) and add three pieces of value-add that the project README does not have in one place:

1. **Lead with the planning-matters callout** so a user copying this file is reminded why every section exists.
2. **A naming-convention note** at the top: rename to `NN-short-description.md` where `NN` is one higher than the highest in `tasks/done/`. Move into `tasks/todo/` to queue.
3. **A `Blocked Conditions` section** between `Acceptance Criteria` and `Notes` — pre-authorize the runner to bail out and write a `tasks/blocked/` explanation when specific conditions occur (required file missing, baseline tests already failing, contradictory instructions, missing dependency). This matches the contract described in the project README's "Failure and Blocker Handling" section and saves users from re-discovering it.

Keep every other section verbatim aligned with the project README's template (Objective, Scope, Requirements, Constraints, Acceptance Criteria, Notes) so there is one canonical structure for task files in the codebase.

The sample is a **template**, not a runnable task. Make that obvious at the top with a one-line callout: *"This is a template. Copy it into `tasks/todo/`, rename it with the next number, and fill it in. Do not run it as-is."*

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folder structure. Use `xUnit` + `FakeItEasy` + `FluentAssertions`, matching the existing `SetupRepositoryTests`. Write all new tests **before** implementation.

### `SetupRepositoryTests` (existing file — add tests, do not break existing ones)

Add the following tests, each asserting one thing and following the naming style of the existing tests in the file:

1. `WriteSampleTemplateToTasksDirectory` — assert `fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\sample-task-template.md", A<string>._)` was called once.
2. `ReadSampleTemplateFromEmbeddedResource` — assert `readEmbeddedResource.Read("SampleTaskTemplate.md")` was called once.
3. `WriteSampleTemplateContentFromEmbeddedResource` — configure the fake `readEmbeddedResource.Read("SampleTaskTemplate.md")` to return a known sentinel string, assert `fileSystemTools.WriteAllText` was called with that exact string for the sample-template path. Use the same pattern the existing `WriteReadmeToTasksDirectory` test uses with `A<string>.That.Contains(...)`.

Add the matching `Returns` setup to the test class constructor so the fake produces a known body:

```csharp
A.CallTo(() => readEmbeddedResource.Read("SampleTaskTemplate.md")).Returns("# Sample Task Template\n\n...");
```

### Existing tests that must stay green

- `WriteReadmeToTasksDirectory` currently asserts the README content `Contains("# Tasks")`. The rewritten README still starts with `# Tasks`, so this test stays green unchanged. **Do not weaken this assertion** — if the README rewrite changes the heading, update the test, but the rewrite below keeps the heading identical.
- All directory-creation and `.gitkeep` tests are unaffected — folder layout does not change.
- `WriteSettingsJsonToTasksDirectory` and `ReadSettingsFromEmbeddedResource` are unaffected.

### No tests for content quality

Do not assert on the body of the rewritten README or the sample template beyond "non-empty / contains the resource we configured the fake to return." The text is a maintenance-time decision, not a code contract — locking it down with brittle string assertions creates churn every time the docs improve. The csproj-level embedded-resource registration is the contract; the test that the resource is read and written is the contract. Body wording is not.

## Implementation Order

**Phase 1 — Failing tests**
1. Add the three new tests in `SetupRepositoryTests`. Add the matching `A.CallTo(...).Returns(...)` line in the constructor.
2. Run `dotnet test` — confirm exactly the three new tests fail (and the existing `WriteReadmeToTasksDirectory` still passes against the current README).

**Phase 2 — Wire the resource through**
3. Create `CodeWorker/Commands/Setup/SampleTaskTemplate.md` with the body described above (lead with planning-matters, naming-convention note, full template structure, blocked-conditions section, "this is a template" disclaimer).
4. Add `<EmbeddedResource Include="Commands\Setup\SampleTaskTemplate.md" />` to `CodeWorker.csproj` next to the existing two.
5. Add the new `WriteAllText` line to `SetupRepository.Setup` immediately after the existing README write.
6. Run `dotnet test` — all three new tests now pass. Confirm no other tests regressed.

**Phase 3 — Improve the README**
7. Rewrite `CodeWorker/Commands/Setup/TasksReadme.md` to the content described in **README Content** above. Keep the `# Tasks` heading so the existing assertion passes.
8. Run `dotnet test` — everything green.

**Phase 4 — Manual smoke test**
9. Build a fresh publish: `dotnet publish CodeWorker/CodeWorker.csproj -c Release -o <temp-folder>`.
10. Create an empty git repo in a scratch folder and run `<temp-folder>/FatCatCodeWorker.exe setup`.
11. Verify by hand that the new `tasks/sample-task-template.md` exists with the expected body, and `tasks/README.md` has been rewritten with all seven folders.

**Phase 5 — Finish**
12. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`. All clean. No new warnings.

## Constraints

- Do not introduce a new abstraction. Reuse `IReadEmbeddedResource` and `IFileSystemTools` exactly as the existing two writes do. No `IWriteSampleTemplate`, no factory, no helper class. One new line in `SetupRepository.Setup`.
- Do not change [CodeWorker/Commands/RequiredTaskFolders.cs](CodeWorker/Commands/RequiredTaskFolders.cs).
- Do not change [CodeWorker/Commands/Setup/ReadEmbeddedResource.cs](CodeWorker/Commands/Setup/ReadEmbeddedResource.cs). The resource-name prefix logic already supports any file in the `Commands\Setup\` folder once it is marked `<EmbeddedResource>`.
- Do not change [CodeWorker/Commands/Setup/defaultSettings.json](CodeWorker/Commands/Setup/defaultSettings.json).
- Do not edit [tasks/README.md](tasks/README.md) at the root of this repo — it will be regenerated by `setup` from the new embedded resource. Hand-editing it now would be undone the next time `setup` runs.
- Follow every rule in `.claude/rules/csharp/` — primary constructors, block-body methods, file-scoped namespaces, single class per file, `Async` suffix only when overload disambiguation requires it, no `ConfigureAwait(false)`, no comments explaining what code does.

## Acceptance Criteria

- A fresh `FatCatCodeWorker setup` against an empty repo produces a `tasks/` folder containing:
  - All seven subfolders (`todo`, `pending`, `done`, `blocked`, `failed`, `reference`, `logs`), each with a `.gitkeep`.
  - `README.md` — rewritten content listing all seven folders, the planning-matters message, the naming convention, and a pointer to the sample template.
  - `sample-task-template.md` — new file. Body leads with the planning-matters callout, the naming convention, and is followed by the full task template structure.
  - `settings.json` — unchanged.
- `SetupRepository.Setup` makes exactly one additional `IReadEmbeddedResource.Read` call (`"SampleTaskTemplate.md"`) and one additional `IFileSystemTools.WriteAllText` call (`tasks/sample-task-template.md`) compared to before.
- `dotnet build`, `dotnet test`, and `dotnet format` are all clean. No new compiler warnings. CSharpier produces no diff.
- Running `setup` a second time over the same repository overwrites both `README.md` and `sample-task-template.md` with the embedded versions (current behaviour; this task does not change it).

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules in `.claude/rules/csharp` — no exceptions
- [ ] No banned patterns used (see `.claude/rules/csharp/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Manual smoke test: fresh `setup` against an empty scratch repo produces both files with the expected content
- [ ] Report results before finishing
