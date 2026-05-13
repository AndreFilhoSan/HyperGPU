## Summary

-

## Validation

- [ ] `dotnet build .\HyperGPU\HyperGPU.csproj -c Debug -p:Platform=x64`
- [ ] `dotnet test .\HyperGPU.Tests\HyperGPU.Tests.csproj -c Debug -p:Platform=x64`

## Risk area

- [ ] UI only
- [ ] Host readiness checks
- [ ] PowerShell command generation
- [ ] Script patching
- [ ] VM provisioning
- [ ] VHD mount/dismount
- [ ] Password or secret handling
- [ ] Documentation only

## Security checklist

- [ ] This change does not log passwords or secrets.
- [ ] PowerShell inputs are quoted or validated.
- [ ] Destructive operations are scoped to the selected VM/VHD.
- [ ] Tests were added or updated for changed behavior.

## Screenshots

Add screenshots for UI changes.
