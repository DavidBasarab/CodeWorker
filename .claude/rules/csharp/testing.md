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
[Fact] public void BeAPost() { ... }
[Fact] public void ExecuteTheResetToFactory() { ... }
[Fact] public void ReturnOk() { ... }
```

## Test Stack
- Framework: xUnit
- Faking: FakeItEasy (`A.Fake<T>()`, `A.CallTo()`)
- Assertions: FluentAssertions (`.Should()`, `.BeOk()`, `.BePost()`)
- Base class: `BddBase` — always use the non-generic form. Do not use `BddBase<T>`.
- Thread substitute: `FakeThread` (runs IThread operations synchronously in tests)
- Test data: `Faker.Create<T>()` for generating test objects — do not hard-code values

## Global Usings
Each test project has a single `GlobalUsings.cs` file that declares `global using` directives for the test stack. Production code does NOT use global usings.

```csharp
// GlobalUsings.cs — test project only
global using System.Threading.Tasks;
global using FakeItEasy;
global using FatCat.Fakes;
global using FluentAssertions;
global using Haivision.Tests;
global using Xunit;
```

Add project-specific namespaces that appear in nearly every test file in the same project. Do not use global usings in production code.

## Expression-Bodied Members in Tests — BANNED
The expression-bodied member ban applies to test code too. All test methods and constructors must use block bodies:

```csharp
// Wrong
[Fact]
public void ReturnOk() => result.Should().BeOk();

public MyTests() => sut = new MySut(fake);

// Correct
[Fact]
public void ReturnOk()
{
    result.Should().BeOk();
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
- Use `Returns(...)` for static, unchanging responses.
- Use `ReturnsLazily(...)` when the return value needs to vary between tests:

```csharp
// In constructor:
private SomeType currentResult;
A.CallTo(() => repo.Get(...)).ReturnsLazily(() => currentResult);

// In each test — just set the field:
currentResult = new SomeType { ... };
```

- This avoids reconfiguring fakes per test and keeps each test focused on its scenario.
- Document any non-trivial fake behavior so future maintainers understand the intent.

## Test Project Conventions
- Test class name = source class name + `Tests`
- Test namespace mirrors source namespace with `Tests.` prepended
- Example: `Haivision.Initialize.Endpoints.FactoryResetEndpoint` →
  `Tests.Haivision.Initialize.Endpoints.FactoryResetEndpointTests`
- There is always a direct 1-to-1 correspondence between a class and its test class.

## Testing and IThread
- In tests, inject `FakeThread` instead of a real `IThread` implementation.
- This runs async/threaded operations synchronously, giving deterministic test results.
- You do not need to test that an action runs in a new thread — test the action itself.
- For testing sleep/delay behavior, use `IThread` and `FakeThread` directly.

## Low-Level API Implementations — No Unit Tests Required
- Classes that talk directly to a low-level external system do not require unit tests.
- Examples: Win32 P/Invoke wrappers, direct MongoDB driver calls, raw OS or hardware APIs.
- These classes exist to satisfy an interface boundary — the interface is tested via fakes everywhere it is consumed.
- Mark the class with `[ExcludeFromCodeCoverage]` and a `Justification` that explains why.

```csharp
[ExcludeFromCodeCoverage(Justification = "Direct wrapper over the MongoDB driver — no business logic, tested via IMongoRepository fakes in consuming classes.")]
public class MongoRepository(IMongoClient client) : IMongoRepository
{
    // ...
}
```

- The justification must be specific: name the low-level API being wrapped and confirm there is no testable business logic in the class.
- Do not apply this exemption to classes that contain any branching logic or orchestration — extract that logic into a separately tested class first.
