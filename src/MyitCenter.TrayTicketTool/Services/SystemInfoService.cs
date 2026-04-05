using System.Runtime.InteropServices;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class SystemInfoService
{
    public SystemInfo Collect()
    {
        return new SystemInfo
        {
            Hostname = Environment.MachineName,
            Username = Environment.UserName,
            OsVersion = RuntimeInformation.OSDescription,
            OsBuild = Environment.OSVersion.Version.ToString(),
            DomainName = Environment.UserDomainName,
            CollectedAt = DateTime.Now
        };
    }
}
