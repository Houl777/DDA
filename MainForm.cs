using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscreteDeviceAssigner
{
    using Microsoft.Management.Infrastructure;
    using DeviceData = Tuple<Microsoft.HyperV.PowerShell.VirtualMachine, Microsoft.HyperV.PowerShell.VMAssignedDevice>;

    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        //Update the virtual machine and appliance display
        private void UpdateVM()
        {
            listView1.Groups.Clear();
            listView1.Items.Clear();

            //Get a list of virtual machines
            var vms = PowerShellWrapper.GetVM();
            var groups = new List<ListViewGroup>();
            foreach (var vm in vms)
            {
                ListViewGroup group = new ListViewGroup("[" + vm.State + "]" + vm.Name);
                groups.Add(group);
            }

            //Get a list of devices under each virtual machine
            var lviss = new List<ListViewItem>[vms.Count];
            Parallel.For(0, vms.Count, (int i) =>
            {
                var vm = vms[i];
                var group = groups[i];
                lviss[i] = new List<ListViewItem>();
                var lvis = lviss[i];

                foreach (var dd in PowerShellWrapper.GetVMAssignableDevice(vm))
                {
                    var dev = PowerShellWrapper.GetPnpDevice(dd.InstanceID);
                    var fn = PowerShellWrapper.GetPnpDeviceFriendlyName(dd.InstanceID);
                    //string name = dev.CimInstanceProperties["Name"] != null ? dev.CimInstanceProperties["Name"].Value as string : null;
                    string name = fn;
                    string clas = dev.CimInstanceProperties["PnpClass"] != null ? dev.CimInstanceProperties["PnpClass"].Value as string : null;
                    lvis.Add(new ListViewItem(new string[] { name != null ? name : "", clas != null ? clas : "", dd.LocationPath }, group)
                    {
                        Tag = new DeviceData(vm, dd),
                    });
                }
                lvis.Add(new ListViewItem("...", group)
                {
                    Tag = new DeviceData(vm, null),
                });
            });

            //Update the ListView
            listView1.BeginUpdate();
            foreach (var group in groups)
            {
                listView1.Groups.Add(group);
            }
            foreach (var lvis in lviss)
            {
                foreach (var lvi in lvis)
                {
                    listView1.Items.Add(lvi);
                }
            }
            listView1.EndUpdate();
        }

        //Load events
        private async void Form1_Load(object sender, EventArgs e)
        {
            await Task.Delay(1);
            UpdateVM();
        }

        //Call out the right-click menu
        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listView1.SelectedItems.Count != 0)
                {
                    DeviceData data = listView1.SelectedItems[0].Tag as DeviceData;
                    contextMenuStrip.Tag = data;
                    contextMenuStrip.Items[0].Text = data.Item1.Name;
                    contextMenuStrip.Show(sender as Control, e.Location);
                }
            }
        }

        //Right-click menu call out event
        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            DeviceData data = contextMenuStrip.Tag as DeviceData;
            if (data.Item2 == null)
            {
                RemoveDeviceToolStripMenuItem.Enabled = false;
                CopyAddressToolStripMenuItem.Enabled = false;
            }
            else
            {
                RemoveDeviceToolStripMenuItem.Enabled = true;
                CopyAddressToolStripMenuItem.Enabled = true;
            }
            uint lowMMIO = 0;
            try
            {
                //This sentence will inexplicably throw an exception
                lowMMIO = data.Item1.LowMemoryMappedIoSpace;
            }
            catch { }
            LMMIOtoolStripTextBox.Text = (lowMMIO / 1024 / 1024).ToString();
            HMMIOtoolStripTextBox.Text = (data.Item1.HighMemoryMappedIoSpace / 1024 / 1024).ToString();
            GCCTtoolStripMenuItem.Checked = data.Item1.GuestControlledCacheTypes;
        }

        //Add a device
        private void AddDeviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeviceData data = contextMenuStrip.Tag as DeviceData;
            CimInstance dev = new PnpDeviceForm().GetResult();
            if (dev != null)
            {
                string name = dev.CimInstanceProperties["Name"] != null ? dev.CimInstanceProperties["Name"].Value as string : null;
                if (name == null) name = "";
                if (MessageBox.Show("Do you want to add the next device“" + name + "”to the next VM“" + data.Item1.Name + "”?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    try
                    {
                        PowerShellWrapper.AddVMAssignableDevice(data.Item1, dev);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error");
                    }
                    UpdateVM();
                }
            }
        }

        //Remove a device
        private void RemoveDeviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeviceData data = contextMenuStrip.Tag as DeviceData;
            if (MessageBox.Show("Do you want to remove the next device“" + data.Item1.Name + "”from the next VM“" + data.Item2.Name + "”?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    PowerShellWrapper.RemoveVMAssignableDevice(data.Item1, data.Item2);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
                UpdateVM();
            }
        }

        //Copy device address
        private void CopyAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeviceData data = contextMenuStrip.Tag as DeviceData;
            Clipboard.SetText(data.Item2.LocationPath);
        }

        //Refresh list
        private void RefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateVM();
        }

        //GuestControlledCacheTypes
        private void GCCTtoolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeviceData data = contextMenuStrip.Tag as DeviceData;
            try
            {
                PowerShellWrapper.SetGuestControlledCacheTypes(data.Item1, !GCCTtoolStripMenuItem.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        //HighMemoryMappedIoSpace
        private void HMMIOtoolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DeviceData data = contextMenuStrip.Tag as DeviceData;
                ulong mb;
                if (ulong.TryParse(HMMIOtoolStripTextBox.Text, out mb))
                {
                    var vm = data.Item1;
                    ulong bytes = mb * 1024 * 1024;
                    if (bytes != vm.HighMemoryMappedIoSpace && bytes != 0)
                    {
                        try
                        {
                            PowerShellWrapper.SetHighMemoryMappedIoSpace(vm, bytes);
                            //Success
                            contextMenuStrip.Close();
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error");
                        }
                    }
                }

                //Failed
                HMMIOtoolStripTextBox.Text = (data.Item1.HighMemoryMappedIoSpace / 1024 / 1024).ToString();
            }
        }

        //LowMemoryMappedIoSpace
        private void LMMIOtoolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DeviceData data = contextMenuStrip.Tag as DeviceData;
                uint mb;
                if (uint.TryParse(LMMIOtoolStripTextBox.Text, out mb))
                {
                    var vm = data.Item1;
                    uint bytes = mb * 1024 * 1024;
                    uint lowMMIO = 0;
                    try
                    {
                        //This sentence will inexplicably throw an exception
                        lowMMIO = data.Item1.LowMemoryMappedIoSpace;
                    }
                    catch { }
                    if ((lowMMIO == 0 || bytes != lowMMIO) && bytes != 0)
                    {
                        try
                        {
                            PowerShellWrapper.SetLowMemoryMappedIoSpace(vm, bytes);
                            //Success
                            contextMenuStrip.Close();
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error");
                        }
                    }
                }

                //Failed
                LMMIOtoolStripTextBox.Text = (data.Item1.LowMemoryMappedIoSpace / 1024 / 1024).ToString();
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
