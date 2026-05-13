using HyperGPU.Models;
using HyperGPU.Services;

namespace HyperGPU.Tests.Services;

[TestClass]
public sealed class HostInspectionServiceTests
{
    [TestMethod]
    public async Task InspectAsync_WhenJsonContainsSingleGpuObject_MapsGpuAndSwitches()
    {
        const string json = """
            {
                            "OperatingSystemLabel": "Windows 11 25H2 (build 26200)",
              "IsHyperVEnabled": true,
              "IsHypervisorPresent": true,
              "Gpus": {
                "Name": "AMD Radeon RX 7700 XT",
                "Vendor": "Advanced Micro Devices, Inc.",
                "DriverVersion": "32.0.31007.1017"
              },
              "NetworkSwitches": "Default Switch"
            }
            """;

        HostInspectionService service = new(new FakePowerShellService(json), new HostReadinessEvaluator(new FakeResourceService()));

        HostInspectionSnapshot snapshot = await service.InspectAsync(CancellationToken.None);

        Assert.AreEqual(1, snapshot.GpuDevices.Count);
        Assert.AreEqual("AMD Radeon RX 7700 XT", snapshot.GpuDevices[0].Name);
        Assert.AreEqual("Windows 11 25H2 (build 26200)", snapshot.OperatingSystemDescription);
        CollectionAssert.AreEqual(new[] { "Default Switch" }, snapshot.NetworkSwitches.ToArray());
    }

    private sealed class FakePowerShellService : IPowerShellService
    {
        private readonly string _result;

        public FakePowerShellService(string result)
        {
            _result = result;
        }

        public Task<string> InvokeScriptAsync(string script, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }

        public Task<string> InvokeScriptAsync(string script, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            return InvokeScriptAsync(script, cancellationToken);
        }

        public Task<string> InvokeScriptFileAsync(string scriptPath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> InvokeScriptFileAsync(string scriptPath, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            return InvokeScriptFileAsync(scriptPath, cancellationToken);
        }

        public Task<T?> InvokeJsonAsync<T>(string script, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeResourceService : IAppResourceService
    {
        public string GetString(string resourceKey)
        {
            return resourceKey switch
            {
                "OsSupportedTitle" => "Supported Windows build",
                "OsSupportedDescription" => "desc",
                "OsSupportedResolution" => "fix",
                "Os64BitTitle" => "64-bit operating system",
                "Os64BitDescription" => "desc",
                "Os64BitResolution" => "fix",
                "AdminTitle" => "Elevated session",
                "AdminDescription" => "desc",
                "AdminResolution" => "fix",
                "HyperVFeatureTitle" => "Hyper-V feature enabled",
                "HyperVFeatureDescription" => "desc",
                "HyperVFeatureResolution" => "fix",
                "HypervisorTitle" => "Hypervisor present",
                "HypervisorDescription" => "desc",
                "HypervisorResolution" => "fix",
                "GpuTitle" => "GPU detected",
                "GpuDescriptionFormat" => "GPU: {0}",
                "GpuResolution" => "fix",
                "SwitchTitle" => "Virtual switch available",
                "SwitchDescriptionFormat" => "Switches: {0}",
                "SwitchResolution" => "fix",
                "CheckStateReady" => "Ready",
                "CheckStateWarning" => "Warning",
                "CheckStateError" => "Error",
                _ => resourceKey,
            };
        }
    }
}