# Test-Driven Development

## TDD Is Non-Negotiable
- All production code is written test-first. No exceptions (other than logging).
- Tests define the contract. Implementation satisfies the tests.
- Tests are not written after the fact — they define behavior before implementation begins.

## One Test, One Assertion
- Each test verifies exactly one thing.
- A failing test must tell you precisely what broke without investigation.
- Test names are sentences describing the expected behavior.

```csharp
[Fact] public void ResolveTheCommandFromArgs() { ... }
[Fact] public void ExecuteTheResolvedCommand() { ... }
[Fact] public void ReturnDoneWhenExitCodeIsZero() { ... }
```

## Test Stack
- Framework: xUnit
- Faking: FakeItEasy (`A.Fake<T>()`, `A.CallTo()`)
- Assertions: FluentAssertions (`.Should()`, `.Be()`, `.BeEquivalentTo()`, etc.)
- Thread substitute: `FakeThread` (from `FatCat.Toolkit.Threading` — runs `IThread` operations synchronously in tests)
- Test data: `Faker.Create<T>()` (from `FatCat.Fakes`) for generating test objects — do not hard-code values

## Test Class Layout — One Plain Class Per Class Under Test
Each class under test has one plain test class named `<Class>Tests`. There is no abstract base and no `Specs` folder. Fakes and the system under test are held in `private readonly` fields, populated with `A.Fake<T>()` or `Faker.Create<T>()`. The system under test is constructed in the test class constructor, where default fake behaviour is also configured:

```csharp
using FatCat.CodeWorker.Commands;
using Serilog;

namespace Testing.FatCat.CodeWorker;

public class CodeWorkerApplicationTests
{
    private readonly IResolveCommand resolveCommand;
    private readonly ICommand resolvedCommand;
    private readonly ILogger logger;
    private readonly CodeWorkerApplication application;

    public CodeWorkerApplicationTests()
    {
        resolveCommand = A.Fake<IResolveCommand>();
        resolvedCommand = A.Fake<ICommand>();
        logger = A.Fake<ILogger>();

        A.CallTo(() => resolveCommand.Resolve(A<string[]>._)).Returns(resolvedCommand);

        application = new CodeWorkerApplication(resolveCommand, logger);
    }

    [Fact]
    public async Task ResolveTheCommandFromArgs()
    {
        var args = new[] { "setup", @"C:\Projects\my-api" };

        await application.DoWork(args);

        A.CallTo(() => resolveCommand.Resolve(args)).MustHaveHappenedOnceExactly();
    }
}
```

## Global Usings
Each test project has a single `GlobalUsings.cs` file that declares `global using` directives for the test stack. Production projects rely on `ImplicitUsings` instead — they do not carry a `GlobalUsings.cs`.

```csharp
// GlobalUsings.cs — test project
global using System.Threading.Tasks;
global using FakeItEasy;
global using FatCat.CodeWorker;
global using FatCat.Fakes;
global using FluentAssertions;
global using Xunit;
```

Add project-specific namespaces that appear in nearly every test file in the same project.

## Test Method Naming — Verb-First
`[Fact]` methods are named as bare verb phrases describing the observable behaviour, with no `Should`, no underscores, no Given/When/Then:

```csharp
[Fact] public void ResolveTheCommandFromArgs() { ... }
[Fact] public void ExecuteTheResolvedCommand() { ... }
[Fact] public void LogWelcomeMessage() { ... }
[Fact] public void ResolveRunCommandWhenNoArgumentsProvided() { ... }
```

## Expression-Bodied Members in Tests — BANNED
The expression-bodied member ban applies to test code too. All test methods and constructors must use block bodies:

```csharp
// Wrong
[Fact]
public void ExecuteTheResolvedCommand() => A.CallTo(() => resolvedCommand.Execute(args)).MustHaveHappenedOnceExactly();

public MyTests() => sut = new MySut(fake);

// Correct
[Fact]
public void ExecuteTheResolvedCommand()
{
    A.CallTo(() => resolvedCommand.Execute(args)).MustHaveHappenedOnceExactly();
}

public MyTests()
{
    sut = new MySut(fake);
}
```

## Test Setup
- Place common setup in the test class constructor: create fakes, configure default return values, initialize the system under test.
- Keep constructor setup minimal and deterministic. Extract to helper methods if setup becomes large.

## FakeItEasy Patterns
- Use `A<T>._` for argument matchers. Never use `A<T>.Ignored` — they are equivalent, and `A<T>._` is the canonical form in this codebase.
- Use `Returns(...)` for static, unchanging responses.
- Use `ReturnsLazily(...)` when the return value needs to vary between tests:

```csharp
// In constructor:
private TaskOutcome currentOutcome;
A.CallTo(() => classifyTaskResult.Classify(A<ProcessResult>._)).ReturnsLazily(() => currentOutcome);

// In each test — just set the field:
currentOutcome = TaskOutcome.Blocked;
```

- This avoids reconfiguring fakes per test and keeps each test focused on its scenario.
- Document any non-trivial fake behavior so future maintainers understand the intent.

## Test Project Conventions
- Test class name = source class name + `Tests`. There is a direct 1-to-1 correspondence between a class under test and its test class.
- Test namespace mirrors the source namespace with `Testing.` prepended (note: `Testing.`, not `Tests.`).
- Example: `FatCat.CodeWorker.Commands.Info.InfoCommand` →
  `Testing.FatCat.CodeWorker.Commands.Info.InfoCommandTests`.

## Testing and IThread
- In tests, inject `FakeThread` instead of a real `IThread` implementation.
- This runs async/threaded operations synchronously, giving deterministic test results.
- You do not need to test that an action runs in a new thread — test the action itself.
- For testing sleep/delay behavior, use `IThread` and `FakeThread` directly.

## Low-Level API Implementations — No Unit Tests Required
- Classes that talk directly to a low-level external system do not require unit tests.
- Examples: `System.Diagnostics.Process` wrappers, direct `System.IO.File`/`Directory` calls, raw OS APIs.
- These classes exist to satisfy an interface boundary — the interface is tested via fakes everywhere it is consumed.
- Mark the class with `[ExcludeFromCodeCoverage]` and a `Justification` that explains why.

```csharp
[ExcludeFromCodeCoverage(
    Justification = "Direct wrapper over System.Diagnostics.Process — no business logic, tested via IRunProcess fakes in consuming classes."
)]
public class RunProcess(ILogger logger) : IRunProcess
{
    // ...
}
```

- The justification must be specific: name the low-level API being wrapped and confirm there is no testable business logic in the class.
- Do not apply this exemption to classes that contain any branching logic or orchestration — extract that logic into a separately tested class first.
