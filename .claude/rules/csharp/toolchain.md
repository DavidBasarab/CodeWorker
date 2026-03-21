# Toolchain

## CSharpier — Final Formatting Authority
CSharpier is the single source of truth for all C# formatting in this codebase.
- Configuration: `printWidth: 128`, `useTabs: true`, `tabWidth: 4`
- Runs automatically on build and on save.
- **Never fight CSharpier.** If it reformats something, that is correct. Do not manually reformat to avoid it.
- Write readable code — CSharpier handles the rest. Do not pre-format to match what you think CSharpier will do.

## ReSharper / Rider — Profile: CineMassive_Default
The team uses the `CineMassive_Default` ReSharper profile (legacy name, pre-acquisition). It enforces:
- Remove redundant code and unnecessary qualifiers
- `var` everywhere (enforced)
- Fields made `readonly` where possible (enforced)
- **Block bodies only** — expression-bodied members (`=>`) are banned
- String interpolation enforced over concatenation

If ReSharper flags something, address it. Do not suppress warnings without reason.
Suppression format when genuinely necessary:
```csharp
// ReSharper disable once <RuleName> — <reason>
```

## .editorconfig
- Naming conventions are enforced as warnings via `.editorconfig`.
- Namespace must match folder structure — enforced.
- All files should be green (no unresolved warnings) unless suppressed with reason.

## Pre-Commit Hook
A pre-commit hook checks staged `.cs` files only (not React, TypeScript, or PowerShell).
- Runs `csharpier --check` and `roslynator analyze`
- Warns but does NOT block commits
- Goal: all files green before merge

## Expression-Bodied Members — BANNED
This applies to ALL members regardless of access modifier: public, private, protected, internal.
**This ban also applies to test projects** — test methods and constructors must use block bodies too.
Do not write:
```csharp
public string Name => _name;                                    // banned
public void Reset() => Execute();                               // banned
private Command360ServerState CurrentServerState => Get();      // banned
private HaivisionServerType ServerType => GetServerType();      // banned
public void WillReturnOk() => result.Should().BeOk();           // banned — even in tests
public MyTests() => sut = new MyClass();                        // banned — even in test constructors
```
Always use block bodies:
```csharp
public string Name { get { return _name; } }                    // correct
public void Reset() { Execute(); }                              // correct
private Command360ServerState CurrentServerState { get { return Get(); } }         // correct
private HaivisionServerType ServerType { get { return GetServerType(); } }         // correct
public void WillReturnOk() { result.Should().BeOk(); }          // correct — test method
public MyTests() { sut = new MyClass(); }                       // correct — test constructor
```
