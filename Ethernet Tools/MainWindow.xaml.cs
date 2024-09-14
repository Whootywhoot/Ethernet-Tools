using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Security.Principal;
using System.Windows.Media;

namespace Ethernet_Tools
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Models
        public class NetworkAdapter
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Status { get; set; }
            public string IPv4Address { get; set; }
            public int InterfaceIndex { get; set; }
            public string MACAddress { get; set; }
            public string SubnetMask { get; set; }
            public string[] DNSServers { get; set; }
            public bool IsDHCPEnabled { get; set; }
        }

        // Data Collections
        public ObservableCollection<NetworkAdapter> NetworkAdapters { get; set; }
        public ObservableCollection<IPAddressInfo> SavedIPAddresses { get; set; }

        // Selected Items

    private NetworkAdapter _selectedAdapter;
    public NetworkAdapter SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (_selectedAdapter != value)
            {
                _selectedAdapter = value;
                OnPropertyChanged(nameof(SelectedAdapter));
            }
        }
    }




        private IPAddressInfo SelectedIPAddress => (IPAddressInfo)SavedIPListBox?.SelectedItem;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            // Initialize Collections
            NetworkAdapters = new ObservableCollection<NetworkAdapter>();
            SavedIPAddresses = new ObservableCollection<IPAddressInfo>();

            // Set Data Contexts
            NetworkAdaptersDataGrid.ItemsSource = NetworkAdapters;
            SavedIPListBox.ItemsSource = SavedIPAddresses;

            // Load Data
            LoadNetworkAdapters();
            
            LoadSavedIPAddresses();
        }

        #region Menu Button Events

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Visible;
        }

        private void CloseMenuButton_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void OverlayMenuBackground_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void EthernetToolsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("EthernetTools");
            MenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void PingToolsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("PingTools");
            MenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void MiscToolsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("MiscTools");
            MenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowPage(string pageName)
        {
            EthernetToolsPage.Visibility = pageName == "EthernetTools" ? Visibility.Visible : Visibility.Collapsed;
            PingToolsPage.Visibility = pageName == "PingTools" ? Visibility.Visible : Visibility.Collapsed;
            MiscToolsPage.Visibility = pageName == "MiscTools" ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Network Adapter Methods

        private void LoadNetworkAdapters()
        {
            try
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces();
                if (adapters.Length == 0)
                {
                    MessageBox.Show("No network adapters found on this system.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var adapter in adapters)
                {
                    try
                    {
                        var props = adapter.GetIPProperties();
                        string ipv4Address = "N/A";
                        int interfaceIndex = -1;

                        try
                        {
                            interfaceIndex = props.GetIPv4Properties()?.Index ?? -1;
                        }
                        catch (NetworkInformationException)
                        {
                            // Skip this adapter if we can't get its IPv4 properties
                            continue;
                        }

                        string description = adapter.Description;
                        string macAddress = adapter.GetPhysicalAddress().ToString();

                        string subnetMask = "N/A";
                        string[] dnsServers = new string[0];
                        bool isDHCPEnabled = false;

                        foreach (var addr in props.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                ipv4Address = addr.Address.ToString();
                                subnetMask = addr.IPv4Mask.ToString();
                                break;
                            }
                        }

                        dnsServers = props.DnsAddresses
                            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(a => a.ToString())
                            .ToArray();

                        isDHCPEnabled = props.GetIPv4Properties().IsDhcpEnabled;

                        NetworkAdapters.Add(new NetworkAdapter
                        {
                            Name = adapter.Name,
                            Description = description,
                            Status = adapter.OperationalStatus.ToString(),
                            IPv4Address = ipv4Address,
                            InterfaceIndex = interfaceIndex,
                            MACAddress = macAddress,
                            SubnetMask = subnetMask,
                            DNSServers = dnsServers,
                            IsDHCPEnabled = isDHCPEnabled
                        });
                    }
                    catch (Exception adapterEx)
                    {
                        // Log the error for this specific adapter, but continue processing others
                        Console.WriteLine($"Error processing adapter {adapter.Name}: {adapterEx.Message}");
                    }
                }

                if (NetworkAdapters.Count == 0)
                {
                    MessageBox.Show("No valid network adapters found. This may be due to insufficient permissions or network configuration issues.", 
                                    "Warning", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (NetworkInformationException nie) when (nie.ErrorCode == 10043)
            {
                MessageBox.Show("Unable to access network information. This may be due to insufficient permissions or network configuration issues.\n\n" +
                                "Error details: " + nie.Message + "\n\n" +
                                "Please try the following:\n" +
                                "1. Run the application as an administrator\n" +
                                "2. Check your network configuration\n" +
                                "3. Ensure that the Network List Service is running",
                                "Network Access Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error loading network adapters: {ex.Message}\n\nStack Trace: {ex.StackTrace}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        #endregion

        #region Saved IP Addresses Methods

        private void LoadSavedIPAddresses()
        {
            try
            {
                if (File.Exists("SavedIPAddresses.json"))
                {
                    var json = File.ReadAllText("SavedIPAddresses.json");
                    var ipList = JsonConvert.DeserializeObject<ObservableCollection<IPAddressInfo>>(json);
                    if (ipList != null)
                    {
                        foreach (var ip in ipList)
                        {
                            SavedIPAddresses.Add(ip);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading saved IP addresses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSavedIPAddresses()
        {
            try
            {
                var json = JsonConvert.SerializeObject(SavedIPAddresses, Formatting.Indented);
                File.WriteAllText("SavedIPAddresses.json", json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving IP addresses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddIPButton_Click(object sender, RoutedEventArgs e)
        {
            var ipInfoWindow = new IPInfoWindow();
            if (ipInfoWindow.ShowDialog() == true)
            {
                SavedIPAddresses.Add(ipInfoWindow.IPAddressInfo);
                SaveSavedIPAddresses();
            }
        }

        private void RemoveIPButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedIPAddress != null)
            {
                SavedIPAddresses.Remove(SelectedIPAddress);
                SaveSavedIPAddresses();
            }
            else
            {
                MessageBox.Show("Please select an IP address to remove.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Quick Change IP and DHCP Methods

        private void QuickChangeIPButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("This operation requires administrator privileges. Please run the application as an administrator.", 
                                "Insufficient Privileges", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
                return;
            }

            if (SelectedAdapter != null && SelectedIPAddress != null)
            {
                try
                {
                    int interfaceIndex = SelectedAdapter.InterfaceIndex;
                    string newIP = SelectedIPAddress.IPAddress;
                    string subnetMask = SelectedIPAddress.SubnetMask;
                    string gateway = SelectedIPAddress.Gateway;
                    string[] dnsServers = SelectedIPAddress.DNS;

                    string wmiQuery = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {interfaceIndex} AND IPEnabled = True";

                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiQuery))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            // Set IP Address and Subnet Mask
                            var newIPParams = obj.GetMethodParameters("EnableStatic");
                            newIPParams["IPAddress"] = new[] { newIP };
                            newIPParams["SubnetMask"] = new[] { subnetMask };
                            obj.InvokeMethod("EnableStatic", newIPParams, null);

                            // Set Default Gateway
                            var newGateway = obj.GetMethodParameters("SetGateways");
                            newGateway["DefaultIPGateway"] = new[] { gateway };
                            newGateway["GatewayCostMetric"] = new[] { 1 };
                            obj.InvokeMethod("SetGateways", newGateway, null);

                            // Set DNS Servers
                            var newDNS = obj.GetMethodParameters("SetDNSServerSearchOrder");
                            newDNS["DNSServerSearchOrder"] = dnsServers;
                            obj.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                        }
                    }

                    MessageBox.Show("IP address changed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    NetworkAdapters.Clear();
                    LoadNetworkAdapters();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error changing IP address: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a network adapter and an IP address.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DHCPButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAdapter != null)
            {
                try
                {
                    int interfaceIndex = SelectedAdapter.InterfaceIndex;

                    string wmiQuery = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {interfaceIndex} AND IPEnabled = True";

                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiQuery))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            // Enable DHCP
                            obj.InvokeMethod("EnableDHCP", null);

                            // Reset DNS
                            obj.InvokeMethod("SetDNSServerSearchOrder", null);
                        }
                    }

                    MessageBox.Show("Switched to DHCP successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    NetworkAdapters.Clear();
                    LoadNetworkAdapters();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error switching to DHCP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a network adapter.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private bool isDarkTheme = false;

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (isDarkTheme)
            {
                Resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                Resources["TextColor"] = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                Resources["PrimaryColor"] = new SolidColorBrush(Color.FromRgb(41, 128, 185));
            }
            else
            {
                Resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                Resources["TextColor"] = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                Resources["PrimaryColor"] = new SolidColorBrush(Color.FromRgb(52, 152, 219));
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NetworkAdapters == null)
            {
                return;
            }

            string searchText = SearchBox.Text.ToLower();
            var filteredAdapters = NetworkAdapters.Where(adapter => 
                adapter.Name.ToLower().Contains(searchText) || 
                adapter.Description.ToLower().Contains(searchText) ||
                adapter.IPv4Address.ToLower().Contains(searchText)).ToList();
            
            NetworkAdaptersDataGrid.ItemsSource = filteredAdapters;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }

        private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json",
                Title = "Export Configuration"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var config = new
                {
                    SavedIPAddresses = SavedIPAddresses,
                    NetworkAdapters = NetworkAdapters.Select(a => new
                    {
                        a.Name,
                        a.Description,
                        a.IPv4Address,
                        a.SubnetMask,
                        a.IsDHCPEnabled
                    })
                };

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, json);
                MessageBox.Show("Configuration exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Import Configuration"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var config = JsonConvert.DeserializeObject<dynamic>(json);

                    SavedIPAddresses.Clear();
                    foreach (var ip in config.SavedIPAddresses)
                    {
                        SavedIPAddresses.Add(new IPAddressInfo
                        {
                            IPAddress = ip.IPAddress,
                            SubnetMask = ip.SubnetMask,
                            Gateway = ip.Gateway,
                            DNS = ip.DNS.ToObject<string[]>()
                        });
                    }

                    MessageBox.Show("Configuration imported successfully.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    SaveSavedIPAddresses();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing configuration: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
    
}
