using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

public class GpuAdapterTests
{
    [Fact]
    public void Hardware_id_matches_the_device_instance_id_windows_gives_the_gpu()
    {
        // The AN515-57's RTX 3070 Laptop GPU: PCI\VEN_10DE&DEV_249D&SUBSYS_153A1025&REV_A1\4&27C18CFB&0&0008
        var gpu = new GpuAdapter("NVIDIA GeForce RTX 3070 Laptop GPU", 0x10DE, 8UL << 30, 0x19AC3, 0, 0x249D, 0x153A1025, 0xA1);
        Assert.Equal(@"PCI\VEN_10DE&DEV_249D&SUBSYS_153A1025&REV_A1", gpu.PnpHardwareId);
    }

    [Fact]
    public void The_discrete_gpu_is_the_one_with_the_most_memory_even_when_both_are_amd()
    {
        // A Ryzen laptop with a Radeon GPU (AN515-42): the same vendor for both.
        var integrated = new GpuAdapter("AMD Radeon(TM) Vega 8 Graphics", 0x1002, 512UL << 20, 1, 0, 0x15DD);
        var discrete = new GpuAdapter("Radeon RX 560X Series", 0x1002, 4UL << 30, 2, 0, 0x67EF);
        Assert.Same(discrete, GpuAdapters.FindDiscrete([integrated, discrete]));
    }
}
