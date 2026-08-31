# Types & Dependency Injection

## var
- Use `var` as the default for local variable declarations.
- Small methods and good naming make the type obvious from context.
- Use explicit types only when the type is not clear from the right-hand side.

## Nullable Reference Types
- This project builds with `<Nullable>disable</Nullable>`. Nullable reference-type annotations (`string?`, `ILogger?`) are not part of the codebase — do not add them to reference types.
- The `?` annotation is still meaningful on value types (`int?`, `DateTime?`) — use it there only where a value is genuinely optional.
- Do not annotate defensively, and do not sprinkle null checks on values that are always populated. If a value cannot be null in normal usage, treat it as non-null.

## Collection Initialization
- Use collection expressions (`[]`) to initialize collections. Do not use `new List<T>()`, `new T[0]`, or `new Dictionary<K, V>()` when an empty or inline-populated collection is needed.
- The target type drives the actual collection — `List<ReferenceFile> ReferenceFiles { get; set; } = [];` produces an empty list, just like `new List<ReferenceFile>()`, but is shorter and consistent.

```csharp
// Correct — collection expressions
public List<ReferenceFile> ReferenceFiles { get; set; } = [];
public string[] Names { get; set; } = [];
var pending = [task1, task2, task3];

// Wrong — explicit constructor calls
public List<ReferenceFile> ReferenceFiles { get; set; } = new List<ReferenceFile>();
public string[] Names { get; set; } = new string[0];
var pending = new List<Task> { task1, task2, task3 };
```

This applies to property initializers, field initializers, local variables, and method arguments. The exception is when you need a specific concrete type that the target cannot infer (e.g. assigning to `IEnumerable<T>` and needing a `HashSet<T>` specifically) — in that case, name the type explicitly.

## Thread-Safe Collections
- Use `ConcurrentDictionary<TKey, TValue>` for shared mutable state that is accessed across threads.
- Never use a plain `Dictionary` with manual locking for this purpose.

## Lazy Initialization
- The default lazy pattern uses the C# `field` keyword with null-coalescing assignment in a property getter. This is preferred over `Lazy<T>` for ordinary deferred initialization:

```csharp
public IReadOnlyList<string> AllWords
{
    get { return field ??= LoadWords(); }
}
```

- Use `Lazy<T>` only when you genuinely need its thread-safety guarantees (e.g. a value that may be initialized concurrently and must run the factory exactly once). When you do, use the factory constructor overload: `new Lazy<T>(() => ...)`.

## Records — BANNED
- Records are banned. Use classes only.

## Access Modifiers
- Public is the default. Do not add access modifiers to restrict visibility unless there is a specific reason.
- `dotnet format` (via `.editorconfig`) enforces readonly and auto-properties — follow its guidance.

## Constructor Injection Only
- All dependencies are injected via the constructor. No property injection. No setter injection.
- Use primary constructors (C# 12+) as the standard form for all new code. Do not write explicit constructor bodies with `this.field = param` assignments.
- Never use `new` inside a class to instantiate a dependency — ask for it via the constructor.

```csharp
// Correct — primary constructor
public class CommandResolver(
    IRunSetupCommand setupCommand,
    IRunTaskCommand runTaskCommand,
    IRunInfoCommand infoCommand,
    IRunHelpCommand helpCommand
) : IResolveCommand
{
    // setupCommand, runTaskCommand, infoCommand, helpCommand are available directly
}

// Wrong — traditional explicit constructor
public class CommandResolver : IResolveCommand
{
    private readonly IRunSetupCommand setupCommand;
    private readonly IRunTaskCommand runTaskCommand;

    public CommandResolver(IRunSetupCommand setupCommand, IRunTaskCommand runTaskCommand)
    {
        this.setupCommand = setupCommand;
        this.runTaskCommand = runTaskCommand;
    }
}
```

## Autofac Registration — SystemScope Scanning + CodeWorkerModule
Dependency wiring is bootstrapped by FatCat's `SystemScope`, which scans the application assemblies at startup (`Program.Main`) and auto-registers every interface that has a single implementation. A single `CodeWorkerModule : Module` registers only what scanning cannot infer.

### When to register in the module
Only add a registration when scanning cannot resolve the type on its own. Add to the module when:
- There are **multiple implementations of the same interface** and you need to choose or override one
- A specific pre-built instance must be registered (e.g. the configured Serilog `ILogger`)
- The type requires `.SingleInstance()` lifetime that cannot be inferred automatically
- The type requires a factory method for construction (`.Factory` pattern)
- The type is an open generic requiring `RegisterGeneric`

Do NOT add to the module when there is exactly one implementation of the interface in the container — `SystemScope` resolves it automatically.

### Rules
- Always register as the interface: `builder.RegisterType<MyClass>().As<IMyCapability>()`
- Add `.SingleInstance()` only when the type is genuinely stateless and safe to share
- Use `RegisterGeneric` for open generic types: `builder.RegisterGeneric(typeof(MyOperation<,>)).As(typeof(IMyOperation<,>))`
- Mark the module class `[ExcludeFromCodeCoverage]` — it contains no testable logic
- Do not register the concrete type without `.As<IInterface>()` unless there is an explicit reason
- For classes that require a factory method for construction, use a static `.Factory` method on the class and register it via `builder.Register(MyClass.Factory)`

```csharp
[ExcludeFromCodeCoverage]
public class CodeWorkerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var logger = SerilogConfiguration.Initialize();

        // A specific pre-built instance — scanning cannot construct this
        builder.RegisterInstance(logger).As<ILogger>().SingleInstance();
    }
}
```

## LINQ
- Use LINQ for querying and transforming collections. Prefer it over imperative loops.
- Always use method chaining syntax. Never use query syntax (`from x in y where...`).
- CSharpier handles formatting — write readable code and let it format.

## IThread — Threading Abstraction
- Threading and sleep operations use `IThread` (from `FatCat.Toolkit.Threading`). Never use `Task.Delay`, `Thread.Sleep`, or raw `Thread` directly.
- `IThread` is injected via constructor like all other dependencies.
- `FakeThread` (also from `FatCat.Toolkit.Threading`) provides a synchronous substitute for unit tests — see `testing.md`.

## IClock — Time Abstraction
- Where the current time drives testable logic, inject `IClock` and read `clock.UtcNow` instead of calling `DateTime.UtcNow` directly. `SystemClock` is the production implementation; injecting the abstraction is what makes time-sensitive assertions deterministic.
- The exception is low-level or `[ExcludeFromCodeCoverage]` plumbing (e.g. process runners and transcript tailers) where a raw `DateTime.UtcNow` timestamp carries no branching logic.

## SystemScope — Container Bootstrap & Late Resolution
- `SystemScope` (FatCat, `FatCat.Toolkit.Injection`) bootstraps the Autofac container in `Program.Main` (`SystemScope.Initialize(...)`) and resolves the root `CodeWorkerApplication` (`SystemScope.Container.Resolve<CodeWorkerApplication>()`).
- Use it only for that root resolution, or for genuine runtime resolution when a type truly must be chosen at run time.
- Do NOT use `SystemScope` / `ISystemScope` as a service locator to dodge constructor injection. If a class always needs the same dependency, inject it — as `CommandResolver` injects every command it can dispatch to.

## C# 14 / net10 Features
- The target framework is `net10.0` with the default language version.
- The `field` keyword is accepted in property getters for backing-field initialization (`field ??= ...`) and in computed properties that need to cache.
- Extension blocks (`extension(TargetType target) { ... }`) are accepted for grouping multiple extension methods on the same type.
- Use these features where they read more clearly — not gratuitously.

## FatCat Ecosystem
CodeWorker depends on the FatCat toolkit for several core capabilities. Use these instead of rolling your own or pulling in equivalents:
- `IThread` / `FakeThread` (`FatCat.Toolkit.Threading`) — threading and sleep abstraction
- `IFileSystemTools` (`FatCat.Toolkit`) — file system operations
- `ConsoleLog` (`FatCat.Toolkit.Console`) — colour-coded console output
- `SystemScope` / `ISystemScope` (`FatCat.Toolkit.Injection`) — Autofac container bootstrap and late resolution
- `Faker.Create<T>()` (`FatCat.Fakes`) — random test data generation
