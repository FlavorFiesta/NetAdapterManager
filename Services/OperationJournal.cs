using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NetAdapterManager.Models;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Services;

public sealed class OperationRecord : ObservableObject
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Kind { get; set; } = "Apply";
    public string AdapterName { get; set; } = "";
    public string InterfaceGuid { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public bool Succeeded { get; set; }
    public bool RolledBack { get; set; }
    public bool? Verified { get; set; }
    public string Message { get; set; } = "";
    public string ErrorDetail { get; set; } = "";
    public string Warnings { get; set; } = "";
    public NetworkConfig? Before { get; set; }
    public NetworkConfig? Target { get; set; }
    public NetworkConfig? Actual { get; set; }
    public bool? ActualEnabled { get; set; }
    public bool? TargetEnabled { get; set; }
    [JsonIgnore] public string Outcome => RolledBack ? "Restored" : Succeeded && Verified == true ? "Success" : "Unconfirmed";
    [JsonIgnore] public string Summary => $"{Timestamp:g} | {L.T("History." + Kind)} | {L.T("History." + Outcome)}";
    [JsonIgnore] public string Details => $"{Summary}\n{AdapterName}\n{InterfaceGuid}\n{MacAddress}\n{Message}\n{ErrorDetail}\n{Warnings}\n" +
        JsonSerializer.Serialize(new { Succeeded, RolledBack, Verified, Before, Target, Actual, TargetEnabled, ActualEnabled }, OperationJournal.Options);
}

public sealed class OperationJournal
{
    public const int MaximumEntries = 200;
    public const int MaximumBytes = 2 * 1024 * 1024;
    internal static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly JsonStore<List<OperationRecord>> _store;
    private readonly SemaphoreSlim _gate;
    public OperationJournal(string fileName = "operation-history.json")
    {
        _store = new(fileName); _gate = StorageLocks.For(_store.FilePath + ".journal");
    }
    public async Task<List<OperationRecord>> ReadAsync()
    {
        if (File.Exists(_store.FilePath) && new FileInfo(_store.FilePath).Length > MaximumBytes)
            throw new IOException(L.T("History.Invalid"));
        var records = await _store.LoadAsync([]);
        if (records.Count > MaximumEntries || records.Any(r => r is null)) throw new IOException(L.T("History.Invalid"));
        return records;
    }
    public async Task AppendAsync(OperationRecord record)
    {
        await _gate.WaitAsync();
        try
        {
            var records = await ReadAsync();
            // Detach and bound any native/provider error before persisting it.
            var copy = JsonSerializer.Deserialize<OperationRecord>(JsonSerializer.Serialize(record, Options), Options)!;
            static string Limit(string? text) => (text ?? "")[..Math.Min(text?.Length ?? 0, 8192)];
            copy.Message = Limit(copy.Message); copy.ErrorDetail = Limit(copy.ErrorDetail); copy.Warnings = Limit(copy.Warnings);
            records.Insert(0, copy);
            if (records.Count > MaximumEntries) records.RemoveRange(MaximumEntries, records.Count - MaximumEntries);
            while (records.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(records, Options).Length > MaximumBytes) records.RemoveAt(records.Count - 1);
            await _store.SaveAsync(records);
        }
        finally { _gate.Release(); }
    }
}

public sealed record DiagnosticPrivacy(bool HideIdentity = true, bool HideMac = true, bool HideIp = true);
public static class DiagnosticExport
{
    private static string? Label(string? value, OperationRecord record, DiagnosticPrivacy privacy)
    {
        if (value is null) return null;
        if (privacy.HideIp) value = Regex.Replace(value, @"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", "[IP hidden]");
        if (privacy.HideMac)
        {
            value = Regex.Replace(value, @"(?i)(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}", "[MAC hidden]");
            if (!string.IsNullOrWhiteSpace(record.MacAddress)) value = value.Replace(record.MacAddress, "[MAC hidden]", StringComparison.OrdinalIgnoreCase);
            var compact = string.Concat((record.MacAddress ?? "").Where(char.IsAsciiHexDigit));
            if (compact.Length == 12) value = value.Replace(compact, "[MAC hidden]", StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }
    private static object? Config(NetworkConfig? config, DiagnosticPrivacy privacy)
    {
        if (privacy.HideIp || config is null) return null;
        if (!privacy.HideIdentity && !privacy.HideMac) return config;
        // Keep only network value fields, never arbitrary driver-origin text.
        static string? Ip(string? value) => ValidationService.TryParseIPv4(value ?? "", out _) ? value : null;
        return new
        {
            config.IpMode, config.DnsMode, ManagedIPv4Address = Ip(config.ManagedIPv4Address),
            IPv4Addresses = config.IPv4Addresses.Select(a => new { Address = Ip(a.Address), a.PrefixLength, Gateway = Ip(a.Gateway), a.IsManaged }),
            DnsServers = config.DnsServers.Select(Ip)
        };
    }
    public static string Create(IEnumerable<OperationRecord> records, DiagnosticPrivacy privacy) => JsonSerializer.Serialize(new
    {
        FormatVersion = 1,
        AppVersion = typeof(DiagnosticExport).Assembly.GetName().Version?.ToString(3),
        CreatedAt = DateTimeOffset.Now,
        Privacy = privacy,
        // Arbitrary native error text may contain identifiers we cannot safely classify.
        FreeTextOmitted = privacy.HideIdentity || privacy.HideMac || privacy.HideIp,
        Operations = records.Select(r => new
        {
            r.Timestamp, Kind = r.Kind is "Apply" or "Enable" or "Disable" ? r.Kind : "Unknown", r.Succeeded, r.RolledBack, r.Verified,
            AdapterName = privacy.HideIdentity ? null : Label(r.AdapterName, r, privacy),
            InterfaceGuid = privacy.HideIdentity ? null : Label(r.InterfaceGuid, r, privacy),
            MacAddress = privacy.HideMac ? null : Label(r.MacAddress, r, privacy),
            Before = Config(r.Before, privacy), Target = Config(r.Target, privacy),
            Actual = Config(r.Actual, privacy), r.ActualEnabled, r.TargetEnabled,
            Message = privacy.HideIdentity || privacy.HideMac || privacy.HideIp ? null : r.Message,
            ErrorDetail = privacy.HideIdentity || privacy.HideMac || privacy.HideIp ? null : r.ErrorDetail,
            Warnings = privacy.HideIdentity || privacy.HideMac || privacy.HideIp ? null : r.Warnings
        })
    }, OperationJournal.Options);
}
