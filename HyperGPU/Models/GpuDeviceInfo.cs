namespace HyperGPU.Models;

public sealed class GpuDeviceInfo
{
    public GpuDeviceInfo(string name, string description, string vendor, string driverVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name = name;
        Description = description;
        Vendor = vendor;
        DriverVersion = driverVersion;
    }

    public string Name { get; }

    public string Description { get; }

    public string Vendor { get; }

    public string DriverVersion { get; }

    public bool IsNvidia => VendorBrand == GpuVendorBrand.Nvidia;

    public bool IsAmd => VendorBrand == GpuVendorBrand.Amd;

    public bool IsIntel => VendorBrand == GpuVendorBrand.Intel;

    public bool IsGeneric => VendorBrand == GpuVendorBrand.Generic;

    public string VendorBadgeText => VendorBrand switch
    {
        GpuVendorBrand.Nvidia => "N",
        GpuVendorBrand.Amd => "A",
        GpuVendorBrand.Intel => "I",
        _ => "GPU",
    };

    public string VendorDisplayName => VendorBrand switch
    {
        GpuVendorBrand.Nvidia => "NVIDIA",
        GpuVendorBrand.Amd => "AMD",
        GpuVendorBrand.Intel => "Intel",
        _ => string.IsNullOrWhiteSpace(Vendor) ? "Unknown" : Vendor,
    };

    private GpuVendorBrand VendorBrand => DetectVendorBrand(Vendor, Name, Description);

    private static GpuVendorBrand DetectVendorBrand(params string[] values)
    {
        foreach (string value in values)
        {
            if (value.Contains("nvidia", StringComparison.OrdinalIgnoreCase))
            {
                return GpuVendorBrand.Nvidia;
            }

            if (value.Contains("advanced micro devices", StringComparison.OrdinalIgnoreCase)
                || value.Contains(" amd", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("amd", StringComparison.OrdinalIgnoreCase)
                || value.Contains("radeon", StringComparison.OrdinalIgnoreCase))
            {
                return GpuVendorBrand.Amd;
            }

            if (value.Contains("intel", StringComparison.OrdinalIgnoreCase)
                || value.Contains("arc", StringComparison.OrdinalIgnoreCase))
            {
                return GpuVendorBrand.Intel;
            }
        }

        return GpuVendorBrand.Generic;
    }

    private enum GpuVendorBrand
    {
        Generic,
        Nvidia,
        Amd,
        Intel,
    }
}