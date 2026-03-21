# Naming & Structure

## Core Philosophy
- Follow Clean Code principles (Robert C. Martin) and SOLID.
- Methods do one thing. Classes have one responsibility.
- Code reads like prose. Names make intent obvious without reading the implementation.
- Prefer interfaces and polymorphism over if/switch chains.
- Do NOT over-engineer. Do NOT introduce abstractions that do not already exist in this codebase.
- Match the abstraction level and style of the surrounding code.

## Naming Rules
- Avoid abbreviations. Prefer full words so readers never have to guess meaning.
- Acceptable abbreviations: widely recognized acronyms (e.g. `HTTP`, `URL`, `ID`) and any abbreviation that appears among the top 3 Google results for that term. When in doubt, use the full word.
- Names reveal intent. A method name makes it unnecessary to read the body.
- No comments explaining what code does — rename until it is obvious.
- PascalCase: classes, interfaces, methods, properties, constants
- camelCase: local variables, parameters, private fields — no leading underscore
- Private fields prefer `readonly` for dependencies where applicable
- Boolean names read as questions or states: `isReady`, `hasOutputs`, `canRestore`
- String interpolation required — never string concatenation with `+`
- Do NOT suffix method names with `Async` just because they return a `Task`. Name the method after what it does: `Save`, not `SaveAsync`. Only use the `Async` suffix when a non-async overload with the same name already exists and both must coexist.

## Discards
- Use `_` to discard outputs you intentionally do not need — `out _` for ignored out parameters, `using var _ = ...` for disposables acquired only for their side effect.

## Method Size
- Methods should be as short as possible.
- ~10 lines is a signal to evaluate refactoring — not an automatic rule.
- No method should require a comment to explain what it does. Refactor or rename instead.

## Spacing
- Leave a blank line between method definitions.
- Leave a blank line after variable declarations in a method before logic begins.
- Leave a blank line before return statements.

## Control Flow
- Avoid deep if/else nesting. Prefer guard clauses and early returns to keep the main flow readable.
- Avoid complex nested ternary expressions — prefer clear `if` statements or extract into a well-named method.
- If you need to explain what code does with a comment, first ask whether a better name makes the comment unnecessary.
- Use switch expressions (not if/else chains) when branching on an enum or type. Always include a discard arm `_` that throws `ArgumentOutOfRangeException` for unhandled cases:

```csharp
// Correct — switch expression
var result = assetType switch
{
    AssetType.Image => ProcessImage(asset),
    AssetType.Video => ProcessVideo(asset),
    _ => throw new ArgumentOutOfRangeException(nameof(assetType)),
};

// Wrong — if/else chain
if (assetType == AssetType.Image) result = ProcessImage(asset);
else if (assetType == AssetType.Video) result = ProcessVideo(asset);
```

## Files & Namespaces
- One class per file. File named after the class, never the interface.
- Exception: if a class directly implements a single interface, both may be in the same file — still named after the class.
- Namespace must exactly match the folder path within the project. No exceptions.
- Test project mirrors source project: same folder structure, same namespace with `Tests.` prepended.
- Always use file-scoped namespaces (C# 10+). Never use block-style `namespace X { }`.

```csharp
// Correct — file-scoped
namespace Haivision.Manager.AssetManager;

public class DeleteAssetEndpoint { }

// Wrong — block-scoped
namespace Haivision.Manager.AssetManager
{
    public class DeleteAssetEndpoint { }
}
```

## Endpoint Pattern

1. **Return `HaiResult`.** All endpoint action methods return `HaiResult` or `Task<HaiResult>`. Never return raw ASP.NET Core types (`IActionResult`, `Ok<T>()`, etc.).

2. **Interface only when reused.** An endpoint does not need an interface by default. Only add one when another part of the codebase needs to call the endpoint's logic directly (e.g. one service calling into another). When an interface is needed, define it in the same file immediately above the class:

```csharp
// Only add this when something else needs to call RestartServer logic directly
public interface IRestartServer
{
    HaiResult Restart();
}

public class RestartServerEndpoint(IRebootServer rebootServer) : HaivisionApiEndpoint, IRestartServer
{
    ...
}
```

If the endpoint is only ever called via HTTP and nothing injects `IRestartServer`, no interface is needed.

3. **Mutable state fields for request context.** When an endpoint breaks its logic into multiple private helper methods, it may use non-`readonly` private fields to share working state across those methods within a single request. These fields are intentionally mutable and are not injected — they are populated during the request:

```csharp
public class DeleteAssetEndpoint(IMongoRepository<AssetData> mongo) : HaivisionApiEndpoint
{
    private AssetData asset;   // request working state — intentionally NOT readonly

    [HttpDelete("Asset/{assetId}")]
    public async Task<HaiResult> DeleteAsset(string assetId)
    {
        await LoadAsset(assetId);
        if (asset == null) return NotFound();
        return await DeleteLoadedAsset();
    }

    private async Task LoadAsset(string assetId) { asset = await mongo.GetById(assetId); }

    private async Task<HaiResult> DeleteLoadedAsset() { ... }
}
```

This pattern avoids passing many parameters between helper methods. It is only valid within an endpoint class where the lifetime of the object is a single HTTP request.

## Interfaces
- All interfaces use the `I` prefix.
- Interface names describe a capability or action: `IClearDatabase`, `IRunExecuteMacrium`, `IRestoreBackup`.
- NOT: `IDatabase`, `IMacrium`, `IBackupService` — these describe what something is, not what it does.
- Default to narrow, single-purpose interfaces. One interface = one capability.
- Exception: highly cohesive groups (e.g. all REST calls to the same API resource) may be grouped: `IManagerApi`.
- All cross-boundary dependencies must be interfaces: threading, file system, time, external processes, REST clients.
- If something cannot be faked in a test, it is not properly abstracted.