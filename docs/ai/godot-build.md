# Godot Build Notes

Godot is installed through WinGet in the agent environment. The executable is
versioned and is exposed on `PATH`; the old hard-coded
`D:\Programming\3rdParty\Godot_v4.6.3-stable_mono_win64` path is not valid here.

Discover the installed Mono executable instead of assuming a version or install
directory:

```powershell
$godot = (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application).Source
& $godot --headless --path godot/Devilution.Client --quit-after 5
```

The project C# solution should be built with `dotnet build` before the Godot
launch. In this environment, `--build-solutions --quit` can leave a Godot Mono
process running after the build; use the explicit .NET build followed by a
short headless launch for deterministic verification.

The local authoritative harness accepts the discovered path explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File godot/run-local-client.ps1 -Headless -GodotExecutable $godot
```

`godot/run-local-client.ps1` now searches PATH for any versioned Godot Mono
binary, preferring the editor executable and falling back to the console
executable.
