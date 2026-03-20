# Test-Driven Development

## TDD Is Non-Negotiable
- All production code is written test-first. No exceptions (other than logging).
- Tests define the contract. Implementation satisfies the tests.
- Tests are not written after the fact — they define behavior before implementation begins.

## One Test, One Assertion
- Each test verifies exactly one thing.
- A failing test must tell you precisely what broke without investigation.
- Test names are sentences describing the expected behavior.
- Test method names must NOT contain underscores (`_`). Use PascalCase only.

```csharp
[Fact] public void BeAPost() { ... }
[Fact] public void ExecuteTheResetToFactory() { ... }
[Fact] public void ReturnOk() { ... }
```

## Test Stack
- Framework: xUnit
- Faking: FakeItEasy (`A.Fake<T>()`, `A.CallTo()`)
- Assertions: FluentAssertions (`.Should()`)
- Test data: `Faker.Create<T>()` for generating test objects — do not hard-code values

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
- Example: `MyApp.Endpoints.FactoryResetEndpoint` →
  `Tests.MyApp.Endpoints.FactoryResetEndpointTests`
- There is always a direct 1-to-1 correspondence between a class and its test class.

## Testing and IThread
- In tests, inject `FakeThread` instead of a real `IThread` implementation.
- This runs async/threaded operations synchronously, giving deterministic test results.
- You do not need to test that an action runs in a new thread — test the action itself.
- For testing sleep/delay behavior, use `IThread` and `FakeThread` directly.
