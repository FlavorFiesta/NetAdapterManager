namespace NetAdapterManager.Models;

public enum IpMode
{
    Dhcp,
    Static
}

public enum DnsMode
{
    Automatic,
    Static
}

public enum AdapterType
{
    Physical,
    Virtual,
    Vpn,
    Other
}

public enum OperationSource
{
    Manual,
    Preset
}
