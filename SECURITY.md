# Security Policy

HyperGPU coordinates Hyper-V and PowerShell operations that may run with administrator privileges. Security reports are taken seriously.

## Supported versions

HyperGPU is currently an early preview. Security fixes target the default branch until stable releases are published.

## Reporting a vulnerability

Please do not disclose vulnerabilities publicly before maintainers have had time to review and fix them.

When the GitHub repository is public, report vulnerabilities through GitHub Security Advisories if enabled. Until then, contact the primary maintainer directly through the repository owner profile.

Include:

- Affected version or commit.
- Reproduction steps.
- Expected and actual behavior.
- Security impact.
- Whether administrator privileges are required.
- Any relevant logs with secrets removed.

## Security expectations

The project should:

- Avoid logging guest passwords or secrets.
- Quote PowerShell-generated values safely.
- Keep privileged operations behind explicit user actions.
- Treat local automation assets as trusted input only.
- Avoid destructive cleanup outside the selected VM/VHD scope.
- Test script patching and generated commands.
- Prefer least-surprise behavior for VM stop, mount, dismount, and restart operations.

## Out of scope

The following are generally out of scope unless they demonstrate a HyperGPU-specific vulnerability:

- Vulnerabilities in Windows, Hyper-V, GPU drivers, or third-party remote desktop tools.
- Issues requiring physical access and no HyperGPU-specific behavior.
- Reports against repositories or systems not owned by the reporter or maintainers.
