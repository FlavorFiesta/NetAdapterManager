using System.Security.Principal;
namespace NetAdapterManager.Services;
public static class PrivilegeService
{
    public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
}
