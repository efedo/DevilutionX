# MSVC Build Notes for Agents

Use the Visual Studio developer environment. Plain PowerShell may find `cl.exe`
or CMake but still miss standard MSVC headers such as `cstdint`, `array`, or
`Windows.h`.

The current working command prefix is:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && <command>'
```

The Visual Studio bundled CMake used by this workspace is:

```bat
"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
```

Configure `x64-Debug` from a clean tree:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DDISCORD_INTEGRATION=True -DBUILD_TESTING=ON'
```

Build the game:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target devilutionx'
```

Build a focused test target:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target debug_console_history_test && build\x64-Debug\debug_console_history_test.exe'
```

Run CTest after test binaries are built:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe" --test-dir build\x64-Debug --output-on-failure'
```

Avoid using `--target all` as a shortcut for the test suite unless benchmarks are
also intended; this target builds benchmark executables too. To build only the
registered GTest executables, use the target names listed in `CMake/Tests.cmake`
under `tests` and `standalone_tests`.

If CMake regeneration reports dubious repository ownership, use repository-local
safe-directory flags where possible:

```bat
git -c safe.directory=D:/Programming/DevilutionX status --short
```

If vcpkg reports permission errors under `%LOCALAPPDATA%\vcpkg`, the command
must run with permission to access the user vcpkg registry cache.
