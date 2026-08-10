using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscreteDeviceAssigner
{
    public partial class PnpDeviceForm : Form
    {
        public PnpDeviceForm()
        {
            InitializeComponent();
        }

        public CimInstance GetResult()
        {
            ShowDialog();
            return Retval;
        }

        private Collection<CimInstance> Devices;
        private string Search = null;
        private CimInstance Retval = null;

        private void UpdateDevices()
        {
            if (Devices == null)
                return;
                
            listView1.BeginUpdate();
            listView1.Items.Clear();
            foreach (var dev in Devices)
            {
                if (dev == null)
                    continue;
                    
                var deviceIdProp = dev.CimInstanceProperties["DeviceId"];
                if (deviceIdProp == null || deviceIdProp.Value == null)
                    continue;
                    
                string id = deviceIdProp.Value as string;
                if (id == null) continue;

                string status = dev.CimInstanceProperties["Status"] != null ? dev.CimInstanceProperties["Status"].Value as string : null;
                string clas = dev.CimInstanceProperties["PnpClass"] != null ? dev.CimInstanceProperties["PnpClass"].Value as string : null;
                string name = dev.CimInstanceProperties["Name"] != null ? dev.CimInstanceProperties["Name"].Value as string : null;

                if ((Search != null && name != null && name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0) || Search == null)
                {
                    listView1.Items.Add(new ListViewItem(new string[] { status == null ? "" : status, clas == null ? "" : clas, name == null ? "" : name, id })
                    {
                        Tag = dev,
                    });
                }
            }
            listView1.EndUpdate();
        }

        private void PnpDeviceForm_Load(object sender, EventArgs e)
        {
            try
            {
                Devices = PowerShellWrapper.GetPnpDevice();
                UpdateDevices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading devices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Devices = new Collection<CimInstance>();
                UpdateDevices();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text.Length == 0)
            {
                Search = null;
            }
            else
            {
                Search = textBox1.Text;
            }
            UpdateDevices();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                CimInstance dev = listView1.SelectedItems[0].Tag as CimInstance;
                if (dev != null)
                {
                    Retval = dev;
                    this.Close();
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
