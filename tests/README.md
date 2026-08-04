# Automated tests

Test suites are organized by responsibility:

- `Unit`: rule and data-structure behavior without scene loading.
- `Integration`: Godot scene and autoload behavior.
- `Determinism`: input, snapshot, hash, rollback, and resimulation.
- `ContentValidation`: stable IDs, required resources, paths, animation names, and localization keys.

Run the C# suites from the repository root:

```powershell
dotnet test FightersThroughTime.csproj --settings .runsettings --nologo
```

Godot scene tests additionally require the pinned GdUnit4 addon and the Godot 4.7.1 .NET executable. The root project is the Debug test host so the adapter launches Godot beside `project.godot`; test code and test-only packages are excluded from non-Debug builds.
