<<<<<<< HEAD
<<<<<<< HEAD
<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.HyperV.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using System.Text.RegularExpressions;

namespace DiscreteDeviceAssigner
{
    /// <summary>
    /// Результат выполнения PowerShell-команды
    /// </summary>
    public class PowerShellResult<T>
    {
        public T Data { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        
        public static PowerShellResult<T> SuccessResult(T data) => new PowerShellResult<T> { Data = data, Success = true };
        public static PowerShellResult<T> FailureResult(string error) => new PowerShellResult<T> { Success = false, ErrorMessage = error };
    }

    class PowerShellWrapper
    {
        /// <summary>
        /// Экранирует строку для безопасного использования в PowerShell-скрипте
        /// </summary>
        private static string EscapePowerShellArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return string.Empty;
            
            // Экранируем обратные кавычки, двойные кавычки и знак доллара
            return Regex.Replace(argument, @"([`""$])", @"`$1");
        }

        private static Collection<PSObject> RunScript(string scriptText)
        {
            try
            {
                using (Runspace runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();
                    Pipeline pipeline = runspace.CreatePipeline();
                    pipeline.Commands.AddScript(scriptText);
                    return pipeline.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PowerShell script execution error: {ex.Message}");
                throw;
            }
        }

        private static async Task<Collection<PSObject>> RunScriptAsync(string scriptText)
        {
            return await Task.Run(() => RunScript(scriptText));
        }

        private static Collection<string> GetPnpDeviceLocationPath(string instanceId)
        {
            Collection<string> results = new Collection<string>();
            
            if (string.IsNullOrEmpty(instanceId))
                return results;
                
            string safeInstanceId = EscapePowerShellArgument(instanceId);
            try
            {
                foreach (var dev in RunScript("Get-PnpDeviceProperty -InstanceId \"" + safeInstanceId + "\" DEVPKEY_Device_LocationPaths"))
                {
                    CimInstance ci = dev.BaseObject as CimInstance; 
                    if (ci == null) continue;
                    
                    var dataProp = ci.CimInstanceProperties["Data"]; 
                    if (dataProp == null) continue;
                    
                    var data = dataProp.Value as IEnumerable<string>; 
                    if (data == null) continue;
                    
                    foreach (var d in data)
                    {
                        results.Add(d);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting PnP device location path for {instanceId}: {ex.Message}");
            }
            return results;
        }

        public static Collection<VirtualMachine> GetVM()
        {
            Collection<VirtualMachine> results = new Collection<VirtualMachine>();
            try
            {
                foreach (var vm in RunScript("Get-VM"))
                {
                    if (vm.BaseObject is VirtualMachine)
                    {
                        results.Add(vm.BaseObject as VirtualMachine);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting VMs: {ex.Message}");
            }
            return results;
        }

        public static Collection<VMAssignedDevice> GetVMAssignableDevice(VirtualMachine vm)
        {
            Collection<VMAssignedDevice> results = new Collection<VMAssignedDevice>();
            
            if (vm == null || string.IsNullOrEmpty(vm.Name))
                return results;
                
            string safeVmName = EscapePowerShellArgument(vm.Name);
            try
            {
                foreach (var vmad in RunScript("Get-VMAssignableDevice -VMName \"" + safeVmName + "\""))
                {
                    if (vmad.BaseObject is VMAssignedDevice)
                    {
                        results.Add(vmad.BaseObject as VMAssignedDevice);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting assignable devices for VM {vm.Name}: {ex.Message}");
            }
            return results;
        }

        public static CimInstance GetPnpDevice(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
                
            string safeInstanceId = EscapePowerShellArgument(instanceId);
            try
            {
                foreach (var dev in RunScript("Get-PnpDevice -InstanceId \"" + safeInstanceId + "\""))
                {
                    if (dev.BaseObject is CimInstance)
                    {
                        return dev.BaseObject as CimInstance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting PnP device {instanceId}: {ex.Message}");
            }
            return null;
        }

        public static string GetPnpDeviceFriendlyName(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
                
            string safeInstanceId = EscapePowerShellArgument(instanceId);
            try
            {
                foreach (var devfn in RunScript(@$"
                                        $instanceID = """ + safeInstanceId + @"""
                                        $instanceID = $instanceID.replace(""PCIP"",""PCI"")
                                        $FindDev = (Get-PnpDevice).Where{{ $_.InstanceId -like $instanceId }}
                                        $Output = $FindDev.FriendlyName.ToString()
                                        $Output"
                    ))
                {
                    return devfn.BaseObject?.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting PnP device friendly name for {instanceId}: {ex.Message}");
            }
            return null;
        }

        public static Collection<CimInstance> GetPnpDevice()
        {
            Collection<CimInstance> results = new Collection<CimInstance>();
            try
            {
                foreach (var dev in RunScript(@"
                $FR = @()
                $pcidevs = Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -like ""PCI*""}
                foreach ($pcidev in $pcidevs) {
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  34"").Data -ne 0) {continue}
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  31"").Data -eq 0) {continue}
                    if ($pcidev.FriendlyName.Contains(""Dismounted"")) {continue}
                    $devtype = ($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  1"").Data
                    if ($devtype -eq 2 -Or $devtype -eq 4 -Or $devtype -eq 5) {} else {continue}
                    $irqA = gwmi -query ""select * from Win32_PnPAllocatedResource"" | Where-Object {$_.__RELPATH -like ""*Win32_IRQResource*""} | Where-Object {$_.Dependent -like ""*"" + $pcidev.PNPDeviceID.Replace(""\"",""\\"") + ""*""}
                    if ($irqA.length -eq 0) {$FR += $pcidev} else {
                        $msiA = $irqA | Where-Object {$_.Antecedent -like ""*IRQNumber=42949*""}
                        if ($msiA.length -eq 0) {continue} else {$FR += $pcidev}
                    }
                }
                $FR"
                ))
            {

                if (dev.BaseObject is CimInstance)
                {
                    results.Add(dev.BaseObject as CimInstance);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting PnP devices: {ex.Message}");
            }
            return results;
        }

        public static void SetGuestControlledCacheTypes(VirtualMachine vm, bool value)
        {
            if (vm == null || string.IsNullOrEmpty(vm.Name))
                return;
                
            string safeVmName = EscapePowerShellArgument(vm.Name);
            try
            {
                if (value)
                {
                    RunScript("Set-VM \"" + safeVmName + "\" -GuestControlledCacheTypes $true");
                }
                else
                {
                    RunScript("Set-VM \"" + safeVmName + "\" -GuestControlledCacheTypes $false");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting GuestControlledCacheTypes for VM {vm.Name}: {ex.Message}");
            }
        }

        public static void SetLowMemoryMappedIoSpace(VirtualMachine vm, uint bytes)
        {
            if (vm == null || string.IsNullOrEmpty(vm.Name))
                return;
                
            string safeVmName = EscapePowerShellArgument(vm.Name);
            try
            {
                RunScript("Set-VM \"" + safeVmName + "\" -LowMemoryMappedIoSpace " + bytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting LowMemoryMappedIoSpace for VM {vm.Name}: {ex.Message}");
            }
        }

        public static void SetHighMemoryMappedIoSpace(VirtualMachine vm, ulong bytes)
        {
            if (vm == null || string.IsNullOrEmpty(vm.Name))
                return;
                
            string safeVmName = EscapePowerShellArgument(vm.Name);
            try
            {
                RunScript("Set-VM \"" + safeVmName + "\" -HighMemoryMappedIoSpace " + bytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting HighMemoryMappedIoSpace for VM {vm.Name}: {ex.Message}");
            }
        }

        public static void RemoveVMAssignableDevice(VirtualMachine vm, VMAssignedDevice device)
        {
            if (vm == null || device == null || string.IsNullOrEmpty(device.LocationPath))
                return;
                
            string safeVmName = EscapePowerShellArgument(vm.Name);
            string safeLocationPath = EscapePowerShellArgument(device.LocationPath);
            string safeInstanceId = EscapePowerShellArgument(device.InstanceID);
            
            try
            {
                RunScript("Remove-VMAssignableDevice -LocationPath \"" + safeLocationPath + "\" -VMName \"" + safeVmName + "\"");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing assignable device from VM {vm.Name}: {ex.Message}");
            }
            try
            {
                RunScript("Mount-VmHostAssignableDevice -LocationPath \"" + safeLocationPath + "\"");
            }
            catch (Exception ex)
            {
                // Игнорируем ошибку размонтирования, устройство может быть уже смонтировано
                System.Diagnostics.Debug.WriteLine($"Warning: Could not mount device: {ex.Message}");
            }
            try
            {
                RunScript("Enable-PnpDevice -InstanceId \"" + safeInstanceId + "\" -Confirm:$false");
            }
            catch (Exception ex)
            {
                // Игнорируем ошибку включения устройства, оно может быть уже включено
                System.Diagnostics.Debug.WriteLine($"Warning: Could not enable device: {ex.Message}");
            }
        }

        public static void AddVMAssignableDevice(VirtualMachine vm, CimInstance device)
        {
            if (vm == null || device == null)
                return;
                
            string id = device.CimInstanceProperties["DeviceId"] != null ? device.CimInstanceProperties["DeviceId"].Value as string : null;

            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("Device ID is null or empty");

            var locationPaths = GetPnpDeviceLocationPath(id);
            if (locationPaths.Count == 0) 
                throw new InvalidOperationException("The specified type of device cannot be added");

            string safeVmName = EscapePowerShellArgument(vm.Name);
            string safeLocationPath = EscapePowerShellArgument(locationPaths[0]);
            string safeInstanceId = EscapePowerShellArgument(id);
            
            try
            {
                if (vm.AutomaticStopAction != StopAction.TurnOff)
                {
                    RunScript("Set-VM -AutomaticStopAction:TurnOff -VMName \"" + safeVmName + "\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not set AutomaticStopAction: {ex.Message}");
            }
            try
            {
                if (vm.DynamicMemoryEnabled && vm.MemoryStartup != vm.MemoryMinimum)
                {
                    RunScript("Set-VM -MemoryStartupBytes:" + vm.MemoryMinimum + " -VMName \"" + safeVmName + "\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not set MemoryStartupBytes: {ex.Message}");
            }
            try
            {
                if (!vm.GuestControlledCacheTypes)
                {
                    SetGuestControlledCacheTypes(vm, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not set GuestControlledCacheTypes: {ex.Message}");
            }

            try
            {
                RunScript("Disable-PnpDevice -InstanceId \"" + safeInstanceId + "\" -Confirm:$false");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not disable device: {ex.Message}");
            }
            try
            {
                RunScript("Dismount-VmHostAssignableDevice -LocationPath \"" + safeLocationPath + "\" -force");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not dismount device: {ex.Message}");
            }
            try
            {
                RunScript("Add-VMAssignableDevice -LocationPath \"" + safeLocationPath + "\" -VMName \"" + safeVmName + "\"");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding assignable device to VM {vm.Name}: {ex.Message}");
                throw;
            }
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.HyperV.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using System.Windows.Forms;

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

        public static string GetPnpDeviceFriendlyName(string instanceId)
        {
            foreach (var devfn in RunScript(@"
                                            $instanceID = " + "\"" + instanceId + "\"" + @"
                                            $instanceID = $instanceID.replace(""PCIP\"",""PCI\"")
                                            $FindDev = (Get-PnpDevice).Where{ $_.InstanceId -like $instanceId }
                                            $Output = $FindDev.FriendlyName.ToString()
                                            $Output"
                ))
            {
                return devfn.BaseObject.ToString();
            }
            return null;
        }

        public static Collection<CimInstance> GetPnpDevice()
        {
            Collection<CimInstance> results = new Collection<CimInstance>();
            foreach (var dev in RunScript(@"
                $FR = @()
                $pcidevs = Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -like ""PCI*""}
                foreach ($pcidev in $pcidevs) {
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  34"").Data -ne 0) {continue}
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  31"").Data -eq 0) {continue}
                    if ($pcidev.FriendlyName.Contains(""Dismounted"")) {continue}
                    $devtype = ($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  1"").Data
                    if ($devtype -eq 2 -Or $devtype -eq 4 -Or $devtype -eq 5) {} else {continue}
                    $irqA = gwmi -query ""select * from Win32_PnPAllocatedResource"" | Where-Object {$_.__RELPATH -like ""*Win32_IRQResource*""} | Where-Object {$_.Dependent -like ""*"" + $pcidev.PNPDeviceID.Replace(""\"",""\\"") + ""*""}
                    if ($irqA.length -eq 0) {$FR += $pcidev} else {
                        $msiA = $irqA | Where-Object {$_.Antecedent -like ""*IRQNumber=42949*""}
                        if ($msiA.length -eq 0) {continue} else {$FR += $pcidev}
                    }
                }
                $FR"
                ))
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
            if (locationPaths.Count == 0) throw new InvalidOperationException("The specified type of device cannot be added");

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
>>>>>>> parent of 681bd61 (Update VM device assignment tool with enhanced error handling and security)
=======
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.HyperV.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using System.Windows.Forms;

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

        public static string GetPnpDeviceFriendlyName(string instanceId)
        {
            foreach (var devfn in RunScript(@"
                                            $instanceID = " + "\"" + instanceId + "\"" + @"
                                            $instanceID = $instanceID.replace(""PCIP\"",""PCI\"")
                                            $FindDev = (Get-PnpDevice).Where{ $_.InstanceId -like $instanceId }
                                            $Output = $FindDev.FriendlyName.ToString()
                                            $Output"
                ))
            {
                return devfn.BaseObject.ToString();
            }
            return null;
        }

        public static Collection<CimInstance> GetPnpDevice()
        {
            Collection<CimInstance> results = new Collection<CimInstance>();
            foreach (var dev in RunScript(@"
                $FR = @()
                $pcidevs = Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -like ""PCI*""}
                foreach ($pcidev in $pcidevs) {
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  34"").Data -ne 0) {continue}
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  31"").Data -eq 0) {continue}
                    if ($pcidev.FriendlyName.Contains(""Dismounted"")) {continue}
                    $devtype = ($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  1"").Data
                    if ($devtype -eq 2 -Or $devtype -eq 4 -Or $devtype -eq 5) {} else {continue}
                    $irqA = gwmi -query ""select * from Win32_PnPAllocatedResource"" | Where-Object {$_.__RELPATH -like ""*Win32_IRQResource*""} | Where-Object {$_.Dependent -like ""*"" + $pcidev.PNPDeviceID.Replace(""\"",""\\"") + ""*""}
                    if ($irqA.length -eq 0) {$FR += $pcidev} else {
                        $msiA = $irqA | Where-Object {$_.Antecedent -like ""*IRQNumber=42949*""}
                        if ($msiA.length -eq 0) {continue} else {$FR += $pcidev}
                    }
                }
                $FR"
                ))
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
            if (locationPaths.Count == 0) throw new InvalidOperationException("The specified type of device cannot be added");

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
>>>>>>> parent of 681bd61 (Update VM device assignment tool with enhanced error handling and security)
=======
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.HyperV.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using System.Windows.Forms;

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

        public static string GetPnpDeviceFriendlyName(string instanceId)
        {
            foreach (var devfn in RunScript(@"
                                            $instanceID = " + "\"" + instanceId + "\"" + @"
                                            $instanceID = $instanceID.replace(""PCIP\"",""PCI\"")
                                            $FindDev = (Get-PnpDevice).Where{ $_.InstanceId -like $instanceId }
                                            $Output = $FindDev.FriendlyName.ToString()
                                            $Output"
                ))
            {
                return devfn.BaseObject.ToString();
            }
            return null;
        }

        public static Collection<CimInstance> GetPnpDevice()
        {
            Collection<CimInstance> results = new Collection<CimInstance>();
            foreach (var dev in RunScript(@"
                $FR = @()
                $pcidevs = Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -like ""PCI*""}
                foreach ($pcidev in $pcidevs) {
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  34"").Data -ne 0) {continue}
                    if (($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  31"").Data -eq 0) {continue}
                    if ($pcidev.FriendlyName.Contains(""Dismounted"")) {continue}
                    $devtype = ($pcidev | Get-PnpDeviceProperty ""{3AB22E31-8264-4b4e-9AF5-A8D2D8E33E62}  1"").Data
                    if ($devtype -eq 2 -Or $devtype -eq 4 -Or $devtype -eq 5) {} else {continue}
                    $irqA = gwmi -query ""select * from Win32_PnPAllocatedResource"" | Where-Object {$_.__RELPATH -like ""*Win32_IRQResource*""} | Where-Object {$_.Dependent -like ""*"" + $pcidev.PNPDeviceID.Replace(""\"",""\\"") + ""*""}
                    if ($irqA.length -eq 0) {$FR += $pcidev} else {
                        $msiA = $irqA | Where-Object {$_.Antecedent -like ""*IRQNumber=42949*""}
                        if ($msiA.length -eq 0) {continue} else {$FR += $pcidev}
                    }
                }
                $FR"
                ))
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
            if (locationPaths.Count == 0) throw new InvalidOperationException("The specified type of device cannot be added");

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
>>>>>>> parent of 681bd61 (Update VM device assignment tool with enhanced error handling and security)
