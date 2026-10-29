namespace NetAdapterManager.Models;

// Immutable v1 wire identifiers. Retaining these values keeps existing libraries and backups
// compatible; translated labels must never be written into GroupName or used for comparisons.
public static class GroupIds
{
    public const string All = "全部分组";
    public const string Ungrouped = "未分组";
    public static string Display(string? id) => id switch
    {
        All => L.T("Group.All"),
        Ungrouped or null or "" => L.T("Group.Ungrouped"),
        _ => id
    };
}
