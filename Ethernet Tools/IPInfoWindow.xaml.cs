using System.Windows;

namespace Ethernet_Tools
{
    public partial class IPInfoWindow : Window
    {
        public IPAddressInfo IPAddressInfo { get; set; }

        public IPInfoWindow()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IPAddressTextBox.Text) ||
                string.IsNullOrWhiteSpace(SubnetMaskTextBox.Text))
            {
                MessageBox.Show("IP Address and Subnet Mask are required.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IPAddressInfo = new IPAddressInfo
            {
                IPAddress = IPAddressTextBox.Text,
                SubnetMask = SubnetMaskTextBox.Text,
                Gateway = GatewayTextBox.Text,
                DNS = DNSTextBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}