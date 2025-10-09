# Contributing to GameWatcher V2 Platform

Thank you for contributing! This guide will help you make changes safely and consistently.

## Before You Start

### Required Reading
1. **[AGENTS.md](AGENTS.md)** - Development conventions and guidelines
2. **[docs/design/APPLICATION_ARCHITECTURE.md](docs/design/APPLICATION_ARCHITECTURE.md)** - App responsibilities
3. **[docs/design/README.md](docs/design/README.md)** - Design docs index

### Development Environment
- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows 10+ (for Windows Graphics Capture)
- Git for version control

## Making Changes

### 1. Architecture Changes

**Before modifying any `App.xaml.cs` or `Program.cs`:**

✅ **DO:**
- Check `docs/design/APPLICATION_ARCHITECTURE.md` for mandatory services
- Validate with `docs/design/DI_COMPLIANCE_CHECKLIST.md`
- Test all three apps (Studio, AuthorStudio, Runtime)
- Update documentation if adding new required services

❌ **DON'T:**
- Remove services without checking if they're mandatory
- Assume an app doesn't need a service based on minimal current code
- Create services with `new` instead of using DI
- Use `Console.WriteLine` in shared services (use `ILogger`)

### 2. Creating Game Packs

Follow the reference implementation in `FF1.PixelRemaster/`:

```csharp
// 1. Create detection config
public static class MyGameDetectionConfig
{
    public static TextboxDetectionConfig GetConfig() => new()
    {
        BorderColors = new List<Color> { /* your colors */ },
        TargetSearchArea = new RectangleF(x%, y%, w%, h%),
        MinSize = new Size(width, height),
        MaxSize = new Size(width, height),
        ColorTolerance = 25
    };
}

// 2. Apps register it via DI
services.AddSingleton<ITextboxDetector>(sp =>
{
    var logger = sp.GetService<ILogger<DynamicTextboxDetector>>();
    return new DynamicTextboxDetector(MyGameDetectionConfig.GetConfig(), logger);
});
```

See `docs/design/02-Game-Pack-System.md` for complete details.

### 3. Adding Engine Services

New services in `GameWatcher.Engine` should:

- Use dependency injection
- Accept `ILogger<T>` for logging
- Follow existing patterns (see `DynamicTextboxDetector` as example)
- Include XML documentation comments
- Be interface-based for testability

Example:
```csharp
public interface IMyService
{
    Task<Result> ProcessAsync(Input input);
}

public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<Result> ProcessAsync(Input input)
    {
        _logger.LogInformation("Processing {Input}", input);
        // Implementation
    }
}
```

### 4. Code Style

We use EditorConfig for consistency. Key points:

- **Indentation:** 4 spaces for C#, 2 for JSON/YAML
- **Braces:** Always on new line (`Allman` style)
- **Naming:** 
  - PascalCase for public members
  - camelCase for parameters
  - _camelCase for private fields
  - IPascalCase for interfaces
- **Logging:** Use structured logging with `ILogger<T>`
  ```csharp
  _logger.LogInformation("Found {Count} items in {Time}ms", count, elapsed);
  ```

### 5. Documentation

**Update docs when you:**
- Add a new required service to any app
- Change app responsibilities
- Add new engine interfaces
- Create breaking changes

**Documentation locations:**
- Architecture changes: `docs/design/APPLICATION_ARCHITECTURE.md`
- API changes: `docs/design/03-Engine-API-Specification.md`
- Pack system: `docs/design/02-Game-Pack-System.md`
- User-facing: `docs/user/`

## Testing Changes

### Build Validation
```bash
# Build entire solution
dotnet build GameWatcher-Platform.sln

# Build specific project
dotnet build GameWatcher.Studio/GameWatcher.Studio.csproj

# Build all in Release
dotnet build -c Release
```

### Runtime Validation

Test all three apps if your changes touch shared code:

```bash
# Test Studio (Player)
dotnet run --project GameWatcher.Studio
# ✅ Should: Launch UI, detect game, play audio

# Test AuthorStudio (Creator)
dotnet run --project GameWatcher.AuthorStudio
# ✅ Should: Launch UI, discover dialogue, generate voices

# Test Runtime (Headless)
dotnet run --project GameWatcher.Runtime
# ✅ Should: Output logs, detect game, process dialogue
```

### Smoke Tests

After major changes:
1. Start FF1 in borderless windowed mode
2. Run each app
3. Verify logs show detection working
4. Check for DI exceptions on startup
5. Verify UI responds (Studio/AuthorStudio)

## Git Workflow

### Branch Naming
```
feat/<area>-<description>     # New features
fix/<area>-<description>      # Bug fixes
docs/<description>            # Documentation updates
refactor/<area>-<description> # Code refactoring
```

Examples:
- `feat/engine-voice-effects`
- `fix/studio-audio-timing`
- `docs/pack-creation-guide`
- `refactor/runtime-pipeline`

### Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `refactor`: Code restructuring
- `test`: Adding tests
- `chore`: Maintenance

Examples:
```
feat(engine): add configurable textbox detection

Extracted FF1-specific values into TextboxDetectionConfig.
DynamicTextboxDetector now accepts config via constructor.

Closes #123

---

fix(studio): register GameCaptureService via DI

Studio needs full capture pipeline to play voiceovers.
Added ITextboxDetector and IOcrEngine to DI configuration.

---

docs: add APPLICATION_ARCHITECTURE.md

Defines mandatory services per application to prevent
inferring purpose from current code state.
```

### Pull Request Process

1. **Create branch** from `main`
2. **Make focused changes** (single concern per PR)
3. **Update documentation** if needed
4. **Test thoroughly** (all three apps if touching shared code)
5. **Write clear PR description**:
   ```markdown
   ## Changes
   - Added configurable textbox detection
   - Updated all apps to use FF1DetectionConfig
   
   ## Testing
   - ✅ Studio builds and detects FF1
   - ✅ AuthorStudio discovers dialogue
   - ✅ Runtime processes headless
   
   ## Documentation
   - Updated APPLICATION_ARCHITECTURE.md
   - Added DI_COMPLIANCE_CHECKLIST.md
   ```
6. **Link related issues** if applicable

## Common Pitfalls

### ❌ Removing Required Services
```csharp
// DON'T - Studio needs capture!
.ConfigureServices((context, services) =>
{
    services.AddLogging();
    services.AddSingleton<MainWindow>();
    // Missing: ITextboxDetector, IOcrEngine, GameCaptureService
});
```

### ✅ Complete Service Registration
```csharp
// DO - All mandatory services registered
.ConfigureServices((context, services) =>
{
    services.AddLogging();
    services.AddSingleton<ITextboxDetector>(sp => /* config */);
    services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
    services.AddSingleton<GameCaptureService>();
    services.AddSingleton<MainWindow>();
});
```

### ❌ Manual Service Creation
```csharp
// DON'T - Bypasses DI and logger injection
_detector = new DynamicTextboxDetector(config);
```

### ✅ DI Service Resolution
```csharp
// DO - Proper DI resolution with logging
_detector = serviceProvider.GetRequiredService<ITextboxDetector>();
```

### ❌ Console.WriteLine in Shared Services
```csharp
// DON'T - Doesn't route through logging system
Console.WriteLine($"Found textbox: {rect}");
```

### ✅ Structured Logging
```csharp
// DO - Uses configured logging with structured data
_logger.LogInformation("Found textbox: {Rect}", rect);
```

## Questions?

- **Architecture questions**: Check `docs/design/APPLICATION_ARCHITECTURE.md`
- **Conventions**: See `AGENTS.md`
- **Pack creation**: See `docs/design/02-Game-Pack-System.md`
- **API details**: See `docs/design/03-Engine-API-Specification.md`

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.
