using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.HyperV.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace DiscreteDeviceAssigner
{
    class PowerShellWrapper
    {
        private static Collection<PSObject> RunScript(string scriptText)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                Pipeline pipeline = runspace.CreatePipeline();
                pipeline.Commands.AddScript(scriptText);
                return pipeline.Invoke();
            }
        }

        private static Collection<string> GetPnpDeviceLocationPath(string instanceId)
        {
            Collection<string> results = new Collection<string>();
            foreach (var dev in RunScript("Get-PnpDeviceProperty -InstanceId \"" + instanceId + "\" DEVPKEY_Device_LocationPaths"))
            {
                CimInstance ci = dev.BaseObject as CimInstance; if (ci == null) continue;
                var data = ci.CimInstanceProperties["Data"]; if (data == null) continue;
                var data2 = data.Value as IEnumerable<string>; if (data2 == null) continue;
                foreach (var d in data2)
                {
                    results.Add(d);
                }
            }
            return results;
        }

        public static Collection<VirtualMachine> GetVM()
        {
            Collection<VirtualMachine> results = new Collection<VirtualMachine>();
            foreach (var vm in RunScript("Get-VM"))
            {
                if (vm.BaseObject is VirtualMachine)
                {
                    results.Add(vm.BaseObject as VirtualMachine);
                }
            }
            return results;
        }

        public static Collection<VMAssignedDevice> GetVMAssignableDevice(VirtualMachine vm)
        {
            Collection<VMAssignedDevice> results = new Collection<VMAssignedDevice>();
            foreach (var vmad in RunScript("Get-VMAssignableDevice -VMName \"" + vm.Name + "\""))
            {
                if (vmad.BaseObject is VMAssignedDevice)
                {
                    results.Add(vmad.BaseObject as VMAssignedDevice);
                }
            }
            return results;
        }

        public static CimInstance GetPnpDevice(string instanceId)
        {
            foreach (var dev in RunScript("Get-PnpDevice -InstanceId \"" + instanceId + "\""))
            {
                if (dev.BaseObject is CimInstance)
                {
                    return dev.BaseObject as CimInstance;
                }
            }
            return null;
        }

        public static Collection<CimInstance> GetPnpDevice()
        {
            Collection<CimInstance> results = new Collection<CimInstance>();
            foreach (var dev in RunScript("Invoke-Expression -Command  $([string]([System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String(\"WwBTAHkAcwB0AGUAbQAuAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoAVQBUAEYAOAAuAEcAZQB0AFMAdAByAGkAbgBnACgAWwBTAHkAcwB0AGUAbQAuAEMAbwBuAHYAZQByAHQAXQA6ADoARgByAG8AbQBCAGEAcwBlADYANABTAHQAcgBpAG4AZwAoACgAJwB7ACIAUwBjAHIAaQBwAHQAIgA6ACIASgBFAFoAUwBJAEQAMABnAFEAQwBnAHAARABRAG8AawBjAEcATgBwAFoARwBWADIAYwB5AEEAOQBJAEUAZABsAGQAQwAxAFEAYgBuAEIARQBaAFgAWgBwAFkAMgBVAGcATABWAEIAeQBaAFgATgBsAGIAbgBSAFAAYgBtAHgANQBJAEgAdwBnAFYAMgBoAGwAYwBtAFUAdABUADIASgBxAFoAVwBOADAASQBIAHMAawBYAHkANQBKAGIAbgBOADAAWQBXADUAagBaAFUAbABrAEkAQwAxAHMAYQBXAHQAbABJAEMASgBRAFEAMABrAHEASQBuADAATgBDAG0AWgB2AGMAbQBWAGgAWQAyAGcAZwBLAEMAUgB3AFkAMgBsAGsAWgBYAFkAZwBhAFcANABnAEoASABCAGoAYQBXAFIAbABkAG4ATQBwAEkASABzAE4AQwBpAEEAZwBJAEMAQgBwAFoAaQBBAG8ASwBDAFIAdwBZADIAbABrAFoAWABZAGcAZgBDAEIASABaAFgAUQB0AFUARwA1AHcAUgBHAFYAMgBhAFcATgBsAFUASABKAHYAYwBHAFYAeQBkAEgAawBnAEkAbgBzAHoAUQBVAEkAeQBNAGsAVQB6AE0AUwAwADQATQBqAFkAMABMAFQAUgBpAE4ARwBVAHQATwBVAEYARwBOAFMAMQBCAE8ARQBRAHkAUgBEAGgARgBNAHoATgBGAE4AagBKADkASQBDAEEAegBOAEMASQBwAEwAawBSAGgAZABHAEUAZwBMAFcANQBsAEkARABBAHAASQBIAHQAagBiADIANQAwAGEAVwA1ADEAWgBYADAATgBDAGkAQQBnAEkAQwBCAHAAWgBpAEEAbwBLAEMAUgB3AFkAMgBsAGsAWgBYAFkAZwBmAEMAQgBIAFoAWABRAHQAVQBHADUAdwBSAEcAVgAyAGEAVwBOAGwAVQBIAEoAdgBjAEcAVgB5AGQASABrAGcASQBuAHMAegBRAFUASQB5AE0AawBVAHoATQBTADAANABNAGoAWQAwAEwAVABSAGkATgBHAFUAdABPAFUARgBHAE4AUwAxAEIATwBFAFEAeQBSAEQAaABGAE0AegBOAEYATgBqAEoAOQBJAEMAQQB6AE0AUwBJAHAATABrAFIAaABkAEcARQBnAEwAVwBWAHgASQBEAEEAcABJAEgAdABqAGIAMgA1ADAAYQBXADUAMQBaAFgAMABOAEMAaQBBAGcASQBDAEEAawBaAEcAVgAyAGQASABsAHcAWgBTAEEAOQBJAEMAZwBrAGMARwBOAHAAWgBHAFYAMgBJAEgAdwBnAFIAMgBWADAATABWAEIAdQBjAEUAUgBsAGQAbQBsAGoAWgBWAEIAeQBiADMAQgBsAGMAbgBSADUASQBDAEoANwBNADAARgBDAE0AagBKAEYATQB6AEUAdABPAEQASQAyAE4AQwAwADAAWQBqAFIAbABMAFQAbABCAFIAagBVAHQAUQBUAGgARQBNAGsAUQA0AFIAVABNAHoAUgBUAFkAeQBmAFMAQQBnAE0AUwBJAHAATABrAFIAaABkAEcARQBOAEMAaQBBAGcASQBDAEIAcABaAGkAQQBvAEoARwBSAGwAZABuAFIANQBjAEcAVQBnAEwAVwBWAHgASQBEAEkAcABJAEgAdAA5AEkARwBWAHMAYwAyAFUAZwBlADIAbABtAEkAQwBnAGsAWgBHAFYAMgBkAEgAbAB3AFoAUwBBAHQAWgBYAEUAZwBOAEMAawBnAGUAMwAwAGcAWgBXAHgAegBaAFMAQgA3AGEAVwBZAGcASwBDAFIAawBaAFgAWgAwAGUAWABCAGwASQBDADEAbABjAFMAQQAxAEsAUwBCADcAZgBTAEIAbABiAEgATgBsAEkASAB0ADkASQBHAE4AdgBiAG4AUgBwAGIAbgBWAGwAZgBYADAATgBDAGkAQQBnAEkAQwBBAGsAYQBYAEoAeABRAFMAQQA5AEkARwBkADMAYgBXAGsAZwBMAFgARgAxAFoAWABKADUASQBDAEoAegBaAFcAeABsAFkAMwBRAGcASwBpAEIAbQBjAG0AOQB0AEkARgBkAHAAYgBqAE0AeQBYADEAQgB1AFUARQBGAHMAYgBHADkAagBZAFgAUgBsAFoARgBKAGwAYwAyADkAMQBjAG0ATgBsAEkAaQBCADgASQBGAGQAbwBaAFgASgBsAEwAVQA5AGkAYQBtAFYAagBkAEMAQgA3AEoARgA4AHUAWAAxADkAUwBSAFUAeABRAFEAVgBSAEkASQBDADEAcwBhAFcAdABsAEkAQwBJAHEAVgAyAGwAdQBNAHoASgBmAFMAVgBKAFIAVQBtAFYAegBiADMAVgB5AFkAMgBVAHEASQBuADAAZwBmAEMAQgBYAGEARwBWAHkAWgBTADEAUABZAG0AcABsAFkAMwBRAGcAZQB5AFIAZgBMAGsAUgBsAGMARwBWAHUAWgBHAFYAdQBkAEMAQQB0AGIARwBsAHIAWgBTAEEAaQBLAGkASQBnAEsAeQBBAGsAYwBHAE4AcABaAEcAVgAyAEwAbABCAE8AVQBFAFIAbABkAG0AbABqAFoAVQBsAEUATABsAEoAbABjAEcAeABoAFkAMgBVAG8ASQBsAHcAaQBMAEMASgBjAFgAQwBJAHAASQBDAHMAZwBJAGkAbwBpAGYAUQAwAEsASQBDAEEAZwBJAEcAbABtAEkAQwBnAGsAYQBYAEoAeABRAFMANQBzAFoAVwA1AG4AZABHAGcAZwBMAFcAVgB4AEkARABBAHAASQBIAHMAawBSAGwASQBnAEsAegAwAGcASgBIAEIAagBhAFcAUgBsAGQAbgAwAGcAWgBXAHgAegBaAFMAQgA3AEoARwAxAHoAYQBVAEUAZwBQAFMAQQBrAGEAWABKAHgAUQBTAEIAOABJAEYAZABvAFoAWABKAGwATABVADkAaQBhAG0AVgBqAGQAQwBCADcASgBGADgAdQBRAFcANQAwAFoAVwBOAGwAWgBHAFYAdQBkAEMAQQB0AGIARwBsAHIAWgBTAEEAaQBLAGsAbABTAFUAVQA1ADEAYgBXAEoAbABjAGoAMAAwAE0AagBrADAATwBTAG8AaQBmAFEAMABLAEkAQwBBAGcASQBHAGwAbQBJAEMAZwBrAGIAWABOAHAAUQBTADUAcwBaAFcANQBuAGQARwBnAGcATABXAFYAeABJAEQAQQBwAEkASAB0AGoAYgAyADUAMABhAFcANQAxAFoAWAAwAGcAWgBXAHgAegBaAFMAQgA3AEoARQBaAFMASQBDAHMAOQBJAEMAUgB3AFkAMgBsAGsAWgBYAFoAOQBEAFEAbwBnAEkAQwBBAGcAZgBRADAASwBmAFEAMABLAEoARQBaAFMARABRAG8APQAiAH0AJwAgAHwAIABDAG8AbgB2AGUAcgB0AEYAcgBvAG0ALQBKAHMAbwBuACkALgBTAGMAcgBpAHAAdAApACkAIAB8ACAAaQBlAHgA\"))))"))
            {
                      
                if (dev.BaseObject is CimInstance)
                {
                    results.Add(dev.BaseObject as CimInstance);
                }
            }
            return results;
        }

        public static void SetGuestControlledCacheTypes(VirtualMachine vm, bool value)
        {
            if (value)
            {
                RunScript("Set-VM \"" + vm.Name + "\" -GuestControlledCacheTypes $true");
            }
            else
            {
                RunScript("Set-VM \"" + vm.Name + "\" -GuestControlledCacheTypes $false");
            }
        }

        public static void SetLowMemoryMappedIoSpace(VirtualMachine vm, uint bytes)
        {
            RunScript("Set-VM \"" + vm.Name + "\" -LowMemoryMappedIoSpace " + bytes);
        }

        public static void SetHighMemoryMappedIoSpace(VirtualMachine vm, ulong bytes)
        {
            RunScript("Set-VM \"" + vm.Name + "\" -HighMemoryMappedIoSpace " + bytes);
        }

        public static void RemoveVMAssignableDevice(VirtualMachine vm, VMAssignedDevice device)
        {
            RunScript("Remove-VMAssignableDevice -LocationPath \"" + device.LocationPath + "\" -VMName \"" + vm.Name + "\"");
            try
            {
                RunScript("Mount-VmHostAssignableDevice -LocationPath \"" + device.LocationPath + "\"");
            }
            catch { }
            try
            {
                RunScript("Enable-PnpDevice -InstanceId \"" + device.InstanceID + "\" -Confirm:$false");
            }
            catch { }
        }

        public static void AddVMAssignableDevice(VirtualMachine vm, CimInstance device)
        {
            string id = device.CimInstanceProperties["DeviceId"] != null ? device.CimInstanceProperties["DeviceId"].Value as string : null;

            var locationPaths = GetPnpDeviceLocationPath(id);
            if (locationPaths.Count == 0) throw new InvalidOperationException("无法添加指定类型的设备");

            try
            {
                if (vm.AutomaticStopAction != StopAction.TurnOff)
                {
                    RunScript("Set-VM -AutomaticStopAction:TurnOff -VMName \"" + vm.Name + "\"");
                }
            }
            catch { }
            try
            {
                if (vm.DynamicMemoryEnabled && vm.MemoryStartup != vm.MemoryMinimum)
                {
                    RunScript("Set-VM -MemoryStartupBytes:" + vm.MemoryMinimum + " -VMName \"" + vm.Name + "\"");
                }
            }
            catch { }
            try
            {
                if (!vm.GuestControlledCacheTypes)
                {
                    SetGuestControlledCacheTypes(vm, true);
                }
            }
            catch { }

            try
            {
                RunScript("Disable-PnpDevice -InstanceId \"" + id + "\" -Confirm:$false");
            }
            catch { }
            try
            {
                RunScript("Dismount-VmHostAssignableDevice -LocationPath \"" + locationPaths[0] + "\" -force");
            }
            catch { }
            RunScript("Add-VMAssignableDevice -LocationPath \"" + locationPaths[0] + "\" -VMName \"" + vm.Name + "\"");
        }
    }
}
