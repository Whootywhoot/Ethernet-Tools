using Newtonsoft.Json;

namespace Ethernet_Tools
{
    public class IPAddressInfo
    {
        [JsonProperty("IPAddress")]
        public string IPAddress { get; set; }

        [JsonProperty("SubnetMask")]
        public string SubnetMask { get; set; }

        [JsonProperty("Gateway")]
        public string Gateway { get; set; }

        [JsonProperty("DNS")]
        public string[] DNS { get; set; }

        public override string ToString()
        {
            return IPAddress;
        }
    }
}