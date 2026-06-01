# Contributing to AvilaVault.Hermes

Thank you for considering contributing to AvilaVault.Hermes! 🎉

## 📋 Code of Conduct

This project adheres to a Code of Conduct. By participating, you agree to maintain a respectful and collaborative environment.

## 🚀 How to Contribute

### 1. Report Bugs

If you find a bug, please open an [issue](https://github.com/[your-username]/hermes/issues) including:

- **Clear description** of the problem
- **Steps to reproduce** the bug
- **Expected behavior** vs. **actual behavior**
- **Version** of the package and .NET
- **Sample code** demonstrating the issue (if possible)

### 2. Suggest Improvements

To suggest new features:

1. Check if a similar [issue](https://github.com/[your-username]/hermes/issues) doesn't already exist
2. Open a new issue with the Feature Request template
3. Clearly describe:
   - The problem the feature solves
   - How you imagine it should work
   - Code examples (if applicable)

### 3. Contribute Code

#### Prerequisites

- .NET SDK 10.0+
- Git
- Code editor (VS Code, Visual Studio, Rider)

#### Workflow

1. **Fork the repository**

2. **Clone your fork**
   ```bash
   git clone https://github.com/[your-username]/hermes.git
   cd hermes
   ```

3. **Create a branch**
   ```bash
   git checkout develop
   git checkout -b feature/my-feature
   ```

4. **Make your changes**
   - Follow the [code conventions](#code-conventions)
   - Add tests for new features
   - Maintain code coverage ≥ 40%
   - Update documentation if needed

5. **Run the tests**
   ```bash
   dotnet test
   dotnet test --collect:"XPlat Code Coverage"
   ```

6. **Commit your changes**
   ```bash
   git add .
   git commit -m "feat: add new feature"
   ```
   
   Use [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat:` New feature
   - `fix:` Bug fix
   - `docs:` Documentation changes
   - `test:` Add or modify tests
   - `refactor:` Code refactoring
   - `perf:` Performance improvement
   - `chore:` Maintenance tasks

7. **Push to your fork**
   ```bash
   git push origin feature/my-feature
   ```

8. **Open a Pull Request**
   - Base: `develop`
   - Compare: `feature/my-feature`
   - Fill in the PR template
   - Wait for review

## 📝 Code Conventions

### C# Style Guide

- Use **PascalCase** for classes, methods and public properties
- Use **camelCase** for local variables and parameters
- Use **_camelCase** for private fields
- Prefer `var` when the type is obvious
- Use single-line expressions when appropriate
- Keep methods small and focused (maximum 20-30 lines)
- Add XML documentation for public APIs

### Example

```csharp
/// <summary>
/// Processes a request and returns the response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="request">The request to be processed.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The handler response.</returns>
public async Task<TResponse> Send<TResponse>(
    IRequest<TResponse> request,
    CancellationToken cancellationToken = default)
{
    if (request is null)
        throw new ArgumentNullException(nameof(request));

    var response = await ProcessRequest(request, cancellationToken);
    return response;
}
```

### Tests

- One test per scenario
- Use descriptive names: `MethodName_Scenario_ExpectedBehavior`
- Organize with AAA pattern (Arrange, Act, Assert)
- Use `Fact` for tests without parameters
- Use `Theory` + `InlineData` for parameterized tests

```csharp
[Fact]
public async Task Send_WithValidRequest_ReturnsExpectedResponse()
{
    // Arrange
    var request = new TestRequest("value");
    
    // Act
    var response = await _hermes.Send(request);
    
    // Assert
    Assert.Equal("expected", response);
}
```

## 🧪 Running Tests

### All tests
```bash
dotnet test
```

### With coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Coverage report
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-html" -reporttypes:Html
```

## 📊 Code Coverage

- **Minimum required**: 40%
- **Recommended**: 80%+
- **Ideal**: 90%+

CI/CD will block PRs with coverage < 40%.

## 🔍 Code Review

All PRs go through review. What we check:

- ✅ Code follows conventions
- ✅ Tests are present and passing
- ✅ Adequate code coverage
- ✅ Documentation updated
- ✅ No duplicated code
- ✅ Performance not degraded
- ✅ No unnecessary dependencies

## 📦 Release Process

Versions are automatically managed by CI/CD:

1. **Develop** → Beta versions (`1.0.0-beta.123`)
2. **Main** → Stable versions (`1.0.0`)

Increment versions via manual workflow:
- **Patch**: Bug fixes (`1.0.0` → `1.0.1`)
- **Minor**: New features (`1.0.0` → `1.1.0`)
- **Major**: Breaking changes (`1.0.0` → `2.0.0`)

## 🆘 Need Help?

- 📖 Read the [README.md](README.md)
- 🛠️ Check [SETUP.md](SETUP.md)
- 💬 Open a [Discussion](https://github.com/[your-username]/hermes/discussions)
- 📧 Contact us: [your-email@example.com]

## 🎉 Recognition

Contributors will be listed in [CONTRIBUTORS.md](CONTRIBUTORS.md) and mentioned in releases.

---

Thank you for contributing! 💙