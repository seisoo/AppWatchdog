# Contributing

Thank you for your interest in contributing to AppWatchdog! This guide will help you get started.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)

---

## Code of Conduct

### Our Standards

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on what is best for the community
- Show empathy towards other community members
- Accept constructive criticism gracefully

### Unacceptable Behavior

- Harassment, discrimination, or offensive comments
- Trolling or inflammatory comments
- Personal or political attacks
- Publishing others' private information
- Any conduct inappropriate in a professional setting

---

## How Can I Contribute?

### Reporting Bugs

Found a bug? Please help us fix it!

1. **Search existing issues** to avoid duplicates
2. **Create a new issue** with:
   - Clear, descriptive title
   - Detailed description
   - Steps to reproduce
   - Expected vs. actual behavior
   - Your environment (Windows version, AppWatchdog version)
   - Relevant logs (remove sensitive information)
   - Screenshots if applicable

### Suggesting Features

Have an idea for a new feature?

1. **Search existing issues** to see if it's already suggested
2. **Create a feature request** with:
   - Clear description of the feature
   - Use case / problem it solves
   - Proposed solution (if you have one)
   - Alternative solutions considered
   - Any relevant examples or mockups

### Contributing Code

Want to write code?

1. **Find an issue** to work on:
   - Look for issues labeled `good first issue` for beginners
   - Comment on the issue to let others know you're working on it
2. **Fork the repository**
3. **Create a branch** for your changes
4. **Write your code** (see development setup below)
5. **Test your changes**
6. **Submit a pull request**

### Contributing Documentation

Documentation improvements are always welcome!

- Fix typos or unclear explanations
- Add examples or clarifications
- Update outdated information
- Translate documentation to other languages
- Improve wiki pages

### Helping Others

- Answer questions in issues
- Help troubleshoot problems
- Review pull requests
- Share your experience using AppWatchdog

---

## Development Setup

### Prerequisites

- **Windows 10 or 11** (AppWatchdog is Windows-only)
- **Visual Studio 2022** (Community Edition or higher)
  - Or **JetBrains Rider**
  - Or **VS Code** with C# extension
- **.NET 8 SDK** (or version specified in `global.json`)
- **Git**

### Getting the Code

1. **Fork the repository** on GitHub
2. **Clone your fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/AppWatchdog.git
   cd AppWatchdog
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/seisoo/AppWatchdog.git
   ```

### Building the Solution

#### Using Visual Studio

1. Open `AppWatchdog.sln`
2. Restore NuGet packages (automatic)
3. Build → Build Solution (F6)
4. Set startup project:
   - For UI development: `AppWatchdog.UI.WPF`
   - For Service development: `AppWatchdog.Service`
5. Run (F5)

#### Using Command Line

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build AppWatchdog.Service

# Run service (interactive mode)
dotnet run --project AppWatchdog.Service

# Run UI
dotnet run --project AppWatchdog.UI.WPF
```

### Running in Development

#### Service

**Option 1: Interactive Mode** (Recommended for development)
```bash
dotnet run --project AppWatchdog.Service
```
- Runs in console, not as Windows Service
- Easier debugging
- See logs in real-time

**Option 2: Install as Service**
```bash
dotnet build -c Release
cd AppWatchdog.Service\bin\Release\net8.0
AppWatchdog.Service.exe install
AppWatchdog.Service.exe start
```
- More realistic testing
- Requires admin privileges

#### UI

```bash
dotnet run --project AppWatchdog.UI.WPF
```
- Run as Administrator for service communication
- UI connects to running service via Named Pipes

### Debugging

#### Visual Studio

1. **Service**: Set `AppWatchdog.Service` as startup project, F5 to debug
2. **UI**: Set `AppWatchdog.UI.WPF` as startup project, F5 to debug
3. **Both**: Configure multiple startup projects:
   - Solution → Properties → Multiple Startup Projects
   - Set both Service and UI to "Start"

#### Debugging Tips

- Use breakpoints in health checks, jobs, or UI code
- Watch variables and step through code
- Check Immediate Window for testing expressions
- Use Debug → Windows → Output for debug messages

---

## Project Structure

```
AppWatchdog/
├── AppWatchdog.Shared/          # Shared code
│   ├── Models.cs                # Data models
│   ├── Monitoring/              # WatchedApp, enums
│   ├── Jobs/                    # Job event definitions
│   ├── ConfigStore.cs           # Configuration storage
│   ├── ConfigCrypto.cs          # Encryption utilities
│   ├── PipeProtocol.cs          # IPC protocol
│   └── PipeClient.cs            # IPC client
│
├── AppWatchdog.Service/         # Windows Service
│   ├── Program.cs               # Service entry point
│   ├── Worker.cs                # Main worker
│   ├── Jobs/                    # Job implementations
│   │   ├── JobScheduler.cs      # Job scheduling
│   │   ├── HealthMonitorJob.cs  # Health monitoring
│   │   ├── BackupJob.cs         # Backup execution
│   │   └── ...
│   ├── HealthChecks/            # Health check implementations
│   │   ├── IHealthCheck.cs      # Interface
│   │   ├── ProcessHealthCheck.cs
│   │   ├── HttpHealthCheck.cs
│   │   └── ...
│   ├── Recovery/                # Restart logic
│   ├── Notifications/           # Notification system
│   │   ├── NotificationDispatcher.cs
│   │   └── Notifiers/           # Channel implementations
│   ├── Backups/                 # Backup system
│   ├── Pipe/                    # Named Pipe server
│   └── appsettings.json         # Service configuration
│
├── AppWatchdog.UI.WPF/          # WPF Application
│   ├── App.xaml                 # Application definition
│   ├── MainWindow.xaml          # Main window
│   ├── Views/                   # XAML views
│   ├── ViewModels/              # MVVM view models
│   ├── Services/                # UI services
│   ├── Converters/              # Value converters
│   ├── Localization/            # Language resources
│   └── Images/                  # UI assets
│
├── Directory.Build.props        # Common build properties
├── global.json                  # .NET SDK version
└── AppWatchdog.sln              # Solution file
```

---

## Coding Standards

### General Principles

- **KISS**: Keep it simple, stupid
- **DRY**: Don't repeat yourself
- **SOLID**: Follow SOLID principles
- **Readability**: Code is read more than written
- **Consistency**: Follow existing patterns

### C# Style

Follow standard C# conventions:

```csharp
// Use PascalCase for classes, methods, properties
public class MyClass
{
    public string MyProperty { get; set; }
    
    public void MyMethod()
    {
        // Use camelCase for local variables
        var myVariable = "value";
        
        // Use meaningful names
        int userCount = 10; // Good
        int n = 10; // Bad
    }
}

// Use var for obvious types
var config = new WatchdogConfig(); // Good
WatchdogConfig config = new WatchdogConfig(); // Verbose

// Async methods should end with "Async"
public async Task<bool> CheckHealthAsync()
{
    // ...
}

// Use null-conditional and null-coalescing operators
var name = user?.Name ?? "Unknown";

// Prefer expression-bodied members for simple cases
public bool IsEnabled => config.Enabled;
```

### Naming Conventions

- **Classes**: `PascalCase` (e.g., `HealthMonitorJob`)
- **Interfaces**: `IPascalCase` (e.g., `IHealthCheck`)
- **Methods**: `PascalCase` (e.g., `ExecuteAsync`)
- **Properties**: `PascalCase` (e.g., `CheckInterval`)
- **Fields**: `_camelCase` (private), `PascalCase` (public)
- **Parameters**: `camelCase` (e.g., `userName`)
- **Local variables**: `camelCase` (e.g., `result`)

### Code Organization

- One class per file (generally)
- Group related classes in folders
- Order members: fields, constructors, properties, methods
- Keep methods short and focused
- Extract complex logic into separate methods

### Comments

```csharp
// Use XML comments for public APIs
/// <summary>
/// Checks if the application is healthy.
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Health check result</returns>
public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
{
    // Use inline comments sparingly
    // Only when code isn't self-explanatory
    var timeout = TimeSpan.FromSeconds(30); // Allow extra time for slow services
    
    // Good code is self-documenting
    if (IsServiceRunning())
    {
        return HealthCheckResult.Healthy();
    }
}
```

### Error Handling

```csharp
// Catch specific exceptions
try
{
    await httpClient.GetAsync(url);
}
catch (HttpRequestException ex)
{
    // Log and handle
    _logger.LogError(ex, "HTTP request failed");
    return HealthCheckResult.Unhealthy(ex.Message);
}

// Don't swallow exceptions
catch (Exception ex)
{
    // Bad: catch and ignore
    // Good: log and rethrow or handle appropriately
    _logger.LogError(ex, "Unexpected error");
    throw;
}
```

### Async/Await

```csharp
// Always use async/await for I/O operations
public async Task<string> ReadFileAsync(string path)
{
    return await File.ReadAllTextAsync(path);
}

// Don't block async code
var result = task.Result; // Bad
var result = await task; // Good

// Use ConfigureAwait(false) in libraries
var data = await ReadDataAsync().ConfigureAwait(false);
```

---

## Testing

### Unit Tests

(If test project exists)

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test AppWatchdog.Tests

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Manual Testing

Before submitting changes:

1. **Build in Release mode**: `dotnet build -c Release`
2. **Test service installation**: Install and start service
3. **Test UI**: Launch UI, verify all tabs work
4. **Test your changes**: Verify your specific changes work
5. **Test existing features**: Ensure you didn't break anything
6. **Check logs**: Look for errors or warnings

### Testing Checklist

- [ ] Code compiles without warnings
- [ ] Application starts and runs
- [ ] Your changes work as expected
- [ ] Existing functionality still works
- [ ] No errors in logs
- [ ] UI is responsive
- [ ] Service handles errors gracefully

---

## Submitting Changes

### Commit Messages

Write clear, descriptive commit messages:

```
# Good commit messages
Add HTTP health check with timeout support
Fix service crash when config is corrupted
Update README with installation instructions

# Bad commit messages
fix bug
update code
changes
```

**Format**:
```
Short summary (50 chars or less)

Detailed explanation if needed:
- What changed
- Why it changed
- Any side effects or considerations

Fixes #123
```

### Pull Request Process

1. **Update your fork**:
   ```bash
   git fetch upstream
   git checkout main
   git merge upstream/main
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/my-feature
   # or
   git checkout -b fix/bug-description
   ```

3. **Make your changes**:
   - Write code
   - Test thoroughly
   - Commit with clear messages

4. **Push to your fork**:
   ```bash
   git push origin feature/my-feature
   ```

5. **Create Pull Request**:
   - Go to GitHub
   - Click "New Pull Request"
   - Select your branch
   - Fill in PR template:
     - **Title**: Clear, descriptive title
     - **Description**: What, why, how
     - **Related issues**: Reference issue numbers
     - **Testing**: How you tested
     - **Screenshots**: If UI changes

6. **Respond to feedback**:
   - Reviewers may request changes
   - Make requested changes
   - Push updates to same branch
   - PR updates automatically

### PR Checklist

- [ ] Code follows project style
- [ ] Changes are well-tested
- [ ] Documentation is updated (if needed)
- [ ] Commit messages are clear
- [ ] No merge conflicts
- [ ] PR description is complete

---

## Reporting Bugs

### Before Reporting

1. Search existing issues
2. Verify it's reproducible
3. Test with latest version
4. Gather relevant information

### Bug Report Template

```markdown
**Description**
Clear description of the bug.

**Steps to Reproduce**
1. Step one
2. Step two
3. Step three

**Expected Behavior**
What you expected to happen.

**Actual Behavior**
What actually happened.

**Environment**
- AppWatchdog version: [e.g., 1.0.0]
- Windows version: [e.g., Windows 11 22H2]
- .NET version: [e.g., .NET 8.0.1]

**Logs**
Relevant log excerpts (remove sensitive data).

**Screenshots**
If applicable, add screenshots.

**Additional Context**
Any other relevant information.
```

---

## Suggesting Features

### Feature Request Template

```markdown
**Feature Description**
Clear description of the proposed feature.

**Problem Statement**
What problem does this solve?

**Proposed Solution**
How should this work?

**Alternatives Considered**
What other solutions did you consider?

**Use Cases**
Concrete examples of how this would be used.

**Additional Context**
Any other relevant information, mockups, examples.
```

---

## Questions?

- **Issues**: [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues)
- **Discussions**: [GitHub Discussions](https://github.com/seisoo/AppWatchdog/discussions) (if available)

---

Thank you for contributing to AppWatchdog! Your efforts help make this project better for everyone.

---

[← Back to Home](Home.md)
