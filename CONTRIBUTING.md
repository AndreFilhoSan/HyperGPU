# Contributing to HyperGPU

Thank you for considering a contribution.

HyperGPU is an early-stage Windows/Hyper-V project. Changes that affect VM provisioning, PowerShell execution, administrator flows, VHD mounting, or guest driver handling require extra care.

## Local setup

1. Install the .NET 9 SDK.
2. Use Windows with Hyper-V available for manual provisioning tests.
3. Restore packages:

```powershell
dotnet restore .\HyperGPU.sln
```

4. Build the app:

```powershell
dotnet build .\HyperGPU\HyperGPU.csproj -c Debug -p:Platform=x64
```

5. Run tests:

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet test .\HyperGPU.Tests\HyperGPU.Tests.csproj -c Debug -p:Platform=$Platform
```

## Automation assets

The local `Easy-GPU-PV-main/` folder is ignored. It is used as local inspiration/input material and should not be committed to this repository.

Do not commit generated automation workspaces, VHDs, ISOs, local logs, test outputs, `bin/`, `obj/`, `.venv/`, or `tmp/`.

## Pull request expectations

Before opening a pull request:

- Keep the change focused.
- Add or update tests for service, ViewModel, or script-generation behavior.
- Run the test suite.
- Avoid logging secrets such as guest passwords.
- Explain any change that affects administrator-level operations.
- Include screenshots for UI changes when possible.

## Security-sensitive changes

Treat these areas as security-sensitive:

- PowerShell command generation.
- Script patching.
- VHD mount/dismount behavior.
- BitLocker handling.
- Administrator elevation.
- Password handling.
- File system cleanup.

For these changes, include tests that cover quoting, failure behavior, and log output where practical.
