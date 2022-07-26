using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickNM
{
    public partial class MainForm : Form
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int DestIP, int SrcIP, [Out] byte[] pMacAddr, ref int PhyAddrLen);
        private static Dictionary<string, string> oui = new Dictionary<string, string>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // load oui db
            string[] ouiRawText = Resources.oui.Split('\n');
            if (File.Exists("oui.ini"))
            {
                ouiRawText = File.ReadAllLines("oui.ini");
            }
            foreach (string s in ouiRawText)
            {
                if (s.Trim() == "") { continue; }
                oui.Add(s.Split('=')[0].Trim(), s.Split('=')[1].Split('\\')[0].Trim());
            }
            IPHostEntry localIPs = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in localIPs.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] start = IPv4String2ByteArray(ip.ToString());
                    byte[] end = IPv4String2ByteArray(ip.ToString());
                    start[3] = 0;
                    end[3] = 255;
                    startIPComboBox.Items.Add($"{start[0]}.{start[1]}.{start[2]}.{start[3]}");
                    endIPComboBox.Items.Add($"{end[0]}.{end[1]}.{end[2]}.{end[3]}");
                }
            }
            statusLabel.Text = "Ready.";
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            // Check if IPs are in valid format
            if (!(VerifyIPv4(startIPComboBox.Text) && VerifyIPv4(endIPComboBox.Text)))
            {
                MessageBox.Show("IP is invalid.", "Error");
                return;
            }

            // clear treeview
            deviceListTreeView.Nodes.Clear();

            // Convert IPs to byte[]
            byte[] startIPAddress = IPv4String2ByteArray(startIPComboBox.Text);
            byte[] endIPAddress = IPv4String2ByteArray(endIPComboBox.Text);

            Ping ping = new Ping();

            int totalamount = (endIPAddress[0] - startIPAddress[0]) +
                (endIPAddress[1] - startIPAddress[1]) +
                (endIPAddress[2] - startIPAddress[2]) +
                (endIPAddress[3] - startIPAddress[3]) + 1;

            progressBar.Maximum = totalamount;
            progressBar.Visible = true;

            // god have mercy on us
            for (int i = startIPAddress[0]; i <= endIPAddress[0]; i++)
            {
                for (int j = startIPAddress[1]; j <= endIPAddress[1]; j++)
                {
                    for (int k = startIPAddress[2]; k <= endIPAddress[2]; k++)
                    {
                        for (int l = startIPAddress[3]; l <= endIPAddress[3]; l++)
                        {
                            statusLabel.Text = $"Pinging: {i}.{j}.{k}.{l}";
                            try
                            {
                                PingReply reply = ping.Send($"{i}.{j}.{k}.{l}", 1);
                                if (reply.Address != null)
                                {
                                    deviceListTreeView.Nodes.Add(reply.Address.ToString());
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                            progressBar.Value++;
                            Application.DoEvents();
                        }
                    }
                }
            }

            progressBar.Visible = false;

            // get mac addresses with dark witchcraft (System.Runtime.InteropServices)
            statusLabel.Text = "Searching MAC Addresses.";
            int m = 0;
            foreach (var device in deviceListTreeView.Nodes)
            {
                try
                {
                    IPAddress hostIPAddress = IPAddress.Parse(device.ToString().Replace("TreeNode: ", ""));
                    byte[] ab = new byte[6];
                    int len = ab.Length,
                        r = SendARP((int)hostIPAddress.Address, 0, ab, ref len);
                    string ouiString = oui[BitConverter.ToString(ab, 0, 3).Replace("-", "")];
                    deviceListTreeView.Nodes[m].Nodes.Add($"MAC Address: {BitConverter.ToString(ab, 0, 6)} ({ouiString})");
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.Message);
                }
                m++;
                Application.DoEvents();
            }

            // get hostnames if wanted
            if (resolveHostnameCheckBox.Checked)
            {
                Application.DoEvents();
                statusLabel.Text = "Resolving hostnames (BE PATIENT).";
                int n = 0;
                foreach (var device in deviceListTreeView.Nodes)
                {
                    try
                    {
                        string hostname = "[None]";
                        IPHostEntry entry = Dns.GetHostEntry(device.ToString().Replace("TreeNode: ", ""));
                        if (entry != null)
                        {
                            hostname = entry.HostName;
                        }
                        deviceListTreeView.Nodes[n].Nodes.Add($"Hostname: {hostname}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    n++;
                    Application.DoEvents();
                }
            }
            statusLabel.Text = "Done.";
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            List<string> toSave = new List<string>();
            foreach (var device in deviceListTreeView.Nodes)
            {
                toSave.Add(device.ToString());
            }
        }

        public bool VerifyIPv4(string ip)
        {
            if (ip == null) { return false; }
            if (ip.Trim().Length == 0) { return false; }
            string[] parts = ip.Split('.');
            if (parts.Length != 4) { return false; }
            foreach (string part in parts)
            {
                if (part.Trim().Length == 0) { return false; }
                try { if (int.Parse(part) > 255) { return false; } }
                catch { return false; }
            }
            return true;
        }

        public byte[] IPv4String2ByteArray(string ip)
        {
            byte[] bytes = {
                byte.Parse(ip.Split('.')[0]),
                byte.Parse(ip.Split('.')[1]),
                byte.Parse(ip.Split('.')[2]),
                byte.Parse(ip.Split('.')[3]),
            };
            return bytes;
        }
    }
}
