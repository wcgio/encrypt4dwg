# Repository Guidelines

## Project Structure

This repository contains the Windows 10/11 desktop application `encrypt4dwg`.

- `App.xaml` and `App.xaml.cs` handle application startup and diagnostics.
- `MainWindow.xaml` and its code-behind provide the WPF interface.
- `Models/` contains persistent task models.
- `Services/` contains DPAPI registry storage, AES/RSA encryption, network-time lookup, scheduled checks, and Windows Task Scheduler integration.
- `Assets/ecrypt4Dwg.ico` is the application and installer icon.
- `Installer/ecrypt4Dwg.iss` builds the Inno Setup installer.
- `BUILD.md` documents release and installer generation.

Build outputs (`bin/`, `obj/`, `dist/`) are generated locally and must not be committed.

## Build and Release Commands

Use .NET 8 SDK on Windows. Build and publish from the repository root:

```powershell
dotnet restore -r win-x64
dotnet build -c Release --no-restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --no-restore
```

The published application is `bin\Release\net8.0-windows\win-x64\publish\encrypt4dwg.exe`. Generate the installer with Inno Setup 6:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\ecrypt4Dwg.iss
```

## Coding and Security

Use four-space indentation, file-scoped namespaces, nullable reference types, and PascalCase for C# types and public members. Keep UI event handlers thin; put encryption, filesystem, scheduling, and persistence logic in `Services/`.

Do not weaken AES-GCM authentication, RSA-OAEP-SHA256 wrapping, atomic file writes, or existing overwrite checks. Never commit private PEM keys, encrypted customer drawings, registry data, log files, or unsigned release binaries. Treat the app as a local workflow-control utility, not DRM: documentation must not claim it can prevent pre-expiry copying or administrator bypasses.

## Pull Requests

Use focused Conventional Commit messages, for example `fix(schedule): report startup failures`. PRs should state the affected Windows behavior, validation commands, and any installer changes. Test encryption only with disposable files; verify the private key decrypts the resulting `.locked` file byte-for-byte.
