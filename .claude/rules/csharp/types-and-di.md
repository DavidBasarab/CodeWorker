# Types & Dependency Injection

## var
- Use `var` as the default for local variable declarations.
- Small methods and good naming make the type obvious from context.
- Use explicit types only when the type is not clear from the right-hand side.

## Nullable Reference Types
- Use nullable annotations (`?`) only where a value is genuinely optional or nullable — for example, generic return types (`T?`), reflection-heavy code, or extension methods that accept null input by design.
- Do not annotate defensively. If a value cannot be null in normal usage, do not mark it nullable.
- Do not write `string?`, `ILogger?`, or other nullable annotations on injected dependencies or values that are always populated.

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
- Use `Lazy<T>` for thread-safe singleton initialization when a value is expensive to compute or must be deferred.
- Always use the factory constructor overload: `new Lazy<T>(() => ...)`.

## Records — BANNED
- Records are banned. Use classes only.

## Access Modifiers
- Public is the default. Do not add access modifiers to restrict visibility unless there is a specific reason.
- ReSharper enforces readonly and auto-properties — follow its guidance.

## Constructor Injection Only
- All dependencies are injected via the constructor. No property injection. No setter injection.
- Use primary constructors (C# 12+) as the standard form for all new code. Do not write explicit constructor bodies with `this.field = param` assignments.
- Never use `new` inside a class to instantiate a dependency — ask for it via the constructor.

```csharp
// Correct — primary constructor
public class FactoryResetEndpoint(IRunExecuteMacrium executeMacrium,
                                   IThread thread,
                                   ILogger logger) : HaivisionApiEndpoint
{
    // executeMacrium, thread, logger are available directly
}

// Wrong — traditional explicit constructor
public class FactoryResetEndpoint : HaivisionApiEndpoint
{
    private readonly IRunExecuteMacrium executeMacrium;
    private readonly IThread thread;

    public FactoryResetEndpoint(IRunExecuteMacrium executeMacrium, IThread thread)
    {
        this.executeMacrium = executeMacrium;
        this.thread = thread;
    }
}
```

## Autofac Module Registration
All dependency registration uses Autofac `Module` classes. Each service project has one `*Module : Module` class with a `Load(ContainerBuilder builder)` override.

### When to register in the module
Only add a registration to the module when there are **multiple implementations of the same interface** and you need to override the default. Autofac automatically resolves a single implementation of an interface — no module entry is required for one-to-one mappings.

Add to the module when:
- A service project provides its own implementation of an interface that already has a default implementation elsewhere (e.g. `IDoSomeAction` is implemented in Common as `DoSomeAction`, but this service needs `DoOtherAction` instead — register `DoOtherAction` in the module to override)
- The type requires `.SingleInstance()` lifetime that cannot be inferred automatically
- The type requires a factory method for construction (`.Factory` pattern)
- The type is an open generic requiring `RegisterGeneric`

Do NOT add to the module when:
- There is exactly one implementation of the interface in the container — Autofac resolves it automatically

### Rules
- Always register as the interface: `builder.RegisterType<MyClass>().As<IMyCapability>()`
- Add `.SingleInstance()` only when the type is genuinely stateless and safe to share across all requests
- Use `RegisterGeneric` for open generic types: `builder.RegisterGeneric(typeof(CreateOperation<,,>)).As(typeof(ICreateOperation<,,>))`
- Mark the module class `[ExcludeFromCodeCoverage]` — it contains no testable logic
- Do not register the concrete type without `.As<IInterface>()` unless there is an explicit reason
- For classes that require a factory method for construction, use a static `.Factory` method on the class and register it via `builder.Register(MyClass.Factory)`:
- Use `.OnActivated(handler)` when a type needs initialization that cannot happen in its constructor — for example, calling a method on an open generic after resolution. The handler receives `IActivatedEventArgs<object>` and can use `args.Instance`, `args.Context`, and reflection to invoke the initialization. Use this sparingly; prefer constructor injection for all normal dependencies.

```csharp
// Common project — default implementation, resolved automatically (no module entry needed)
public class DoSomeAction : IDoSomeAction { ... }

// This service project — overrides the default; must be registered in the module
public class DoOtherAction : IDoSomeAction { ... }

[ExcludeFromCodeCoverage]
public class ManagerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Required: overrides the Common default for IDoSomeAction
        builder.RegisterType<DoOtherAction>().As<IDoSomeAction>();

        // Required: SingleInstance lifetime cannot be inferred
        builder.RegisterType<PermissionGroup>().As<IPermissionGroup>().SingleInstance();

        // Required: open generic
        builder.RegisterGeneric(typeof(CreateOperation<,,>)).As(typeof(ICreateOperation<,,>));

        // Required: factory method construction
        builder.Register(CineAgentCache.Factory).SingleInstance();
    }
}
```

## AutoMapper — IConfigureMappings
Each service project has exactly one `*MapperInitialization` class that implements `IConfigureMappings`. It is the single place where all AutoMapper profiles for that service are configured.

Rules:
- Mark the class `[ExcludeFromCodeCoverage]` — it contains no testable logic
- Implement `SetMappings(IProfileExpression configuration)` as the single public method
- Organize mappings into private `Configure*` methods grouped by domain area
- Use `.ConvertUsing<TConverter>()` for complex type conversions that need their own class
- Use `[UsedImplicitly]` on event consumer classes that are invoked via reflection — this suppresses the ReSharper unused-type warning

```csharp
[ExcludeFromCodeCoverage]
public class ManagerMapperInitialization : IConfigureMappings
{
    public void SetMappings(IProfileExpression configuration)
    {
        ConfigureAssetMappings(configuration);
        ConfigureCanvasMappings(configuration);
    }

    private void ConfigureAssetMappings(IProfileExpression configuration)
    {
        configuration.CreateMap<AssetData, AssetServiceModel>();
        configuration.CreateMap<AssetRequest, AssetData>().ConvertUsing<AssetRequestConverter>();
    }

    private void ConfigureCanvasMappings(IProfileExpression configuration)
    {
        configuration.CreateMap<CanvasData, CanvasServiceModel>();
    }
}
```

## LINQ
- Use LINQ for querying and transforming collections. Prefer it over imperative loops.
- Always use method chaining syntax. Never use query syntax (`from x in y where...`).
- CSharpier handles formatting — write readable code and let it format.

## IThread — Threading Abstraction
- Threading and sleep operations use `IThread`. Never use `Task` or `Thread` directly.
- `IThread` is injected via constructor like all other dependencies.
- `FakeThread` provides a synchronous substitute for unit tests.
