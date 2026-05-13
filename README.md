# HyperGPU

HyperGPU is an open-source WinUI 3 application that makes Hyper-V GPU Partitioning easier to configure, validate, and maintain on Windows.

The project turns a script-heavy GPU-PV workflow into a guided desktop experience with host readiness checks, VM configuration, execution previews, live PowerShell logs, safer retry behavior, and existing-VM GPU driver refresh.

## Status

HyperGPU is an early preview. It is intended for advanced Windows users who understand Hyper-V, GPU drivers, and virtual machine recovery.

The app currently uses a hybrid architecture:

- WinUI 3 and MVVM for the desktop experience.
- PowerShell-backed automation for Hyper-V and guest driver operations.
- Automated tests for planning, orchestration, and script patching behavior.

## Why this project exists

GPU-PV can be useful for Windows development, homelab, local streaming, graphics testing, and VM workloads that need GPU acceleration. The setup path is still difficult to understand and easy to break because it involves Hyper-V, VHD mounting, GPU driver files, PowerShell, and host-specific compatibility.

HyperGPU aims to make that workflow more understandable, repeatable, and maintainable.

## Features

- Host readiness checks for Windows, Hyper-V, admin state, GPU devices, network switches, and VMMS.
- Guided VM configuration form.
- Validation summary with field-level visual feedback.
- Execution plan preview before running automation.
- Live execution stages and PowerShell output.
- Automatic creation of missing VHD destination folders.
- Safer cleanup of stale VM/VHD artifacts from failed attempts.
- Non-blocking VMConnect launch.
- Existing-VM guest GPU driver refresh.
- More robust VHD mount handling for driver refresh.
- Support path for already mounted VHDX files and BitLocker-protected guest volumes.
- Automated MSTest coverage for core services and view models.

## Relationship to Easy-GPU-PV

HyperGPU is inspired by the Easy-GPU-PV workflow and uses compatible automation concepts. The `Easy-GPU-PV-main/` folder is intentionally ignored in this repository because it is treated as local inspiration/input material, not as HyperGPU source code.

For local development, place compatible automation assets in `Easy-GPU-PV-main/` at the repository root. The project file includes those assets only when they exist, so the application and tests can still build in clean CI environments without vendoring that folder.

HyperGPU is not an official Easy-GPU-PV project unless stated otherwise by that project's maintainers.

## Requirements

- Windows 10/11 host with Hyper-V support.
- Hyper-V enabled.
- Administrator execution.
- Hyper-V Virtual Machine Management service running.
- Compatible GPU and host driver.
- A Windows ISO from Microsoft.
- .NET 9 SDK for development.
- Visual Studio 2022 or VS Code with the required .NET/WinUI tooling.

## Typical workflow

1. Run HyperGPU as administrator.
2. Refresh host readiness checks.
3. Fix blocking host issues.
4. Select Windows ISO, VHD folder, VM name, CPU, memory, GPU allocation, and network switch.
5. Build the execution plan.
6. Review warnings and generated parameters.
7. Run VM provisioning.
8. Wait for unattended Windows setup to complete inside the VM.
9. Use VMConnect for initial access only.
10. Install a GPU-accelerated remote desktop or game streaming tool inside the guest.
11. Install a virtual display driver inside the guest.
12. Avoid RDP and Hyper-V Enhanced Session for normal GPU-PV use.
13. After host GPU driver updates, use **Update existing VM drivers**.

## Build

```powershell
dotnet restore .\HyperGPU.sln
dotnet build .\HyperGPU\HyperGPU.csproj -c Debug -p:Platform=x64
```

## Test

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet test .\HyperGPU.Tests\HyperGPU.Tests.csproj -c Debug -p:Platform=$Platform
```

## Security model

HyperGPU can orchestrate administrator-level Hyper-V and PowerShell operations. Treat VM provisioning as a privileged workflow.

Security goals:

- Keep automation assets explicit and local.
- Avoid logging guest passwords.
- Quote generated PowerShell inputs.
- Prefer testable service boundaries around PowerShell execution.
- Review changes to provisioning scripts carefully.
- Use automated tests for script generation and VM orchestration behavior.

Report vulnerabilities through the process in `SECURITY.md`.

## Roadmap

- Native C# implementations for more Hyper-V operations.
- More compatibility checks for GPU-PV-capable hardware.
- Better post-provisioning guidance.
- Safer driver refresh for more VHD and BitLocker scenarios.
- CI-backed release packaging.
- Contributor-friendly issue triage and PR review automation.

## Contributing

Contributions are welcome. See `CONTRIBUTING.md` for local setup, testing, and pull request expectations.

## License

HyperGPU is licensed under the MIT License. See `LICENSE`.
