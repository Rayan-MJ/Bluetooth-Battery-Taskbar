using System.Collections.Concurrent;
using Windows.Devices.Enumeration;

namespace BtBattery;

/// <summary>A connected device: name, stable id, and battery percent (null if unknown).</summary>
public record BtDevice(string Id, string Name, int? BatteryPercent);

/// <summary>
/// Keeps a live list of every connected Bluetooth device, with battery where
/// available. Windows exposes those two facts on different device records, so we
/// run two watchers and join them on the device's MAC address:
///
///   Watcher 1 (AssociationEndpoint) — the list of connected devices and names.
///   Watcher 2 (Device nodes)        — battery values, for devices that report one.
///
/// The MAC appears inside both records' ids, so a device is matched to its battery
/// by finding the battery-node id that contains that device's MAC.
/// </summary>
public sealed class BtMonitor
{
    // Windows property holding a device's battery percentage ("{GUID} pid" form).
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string NameKey = "System.ItemNameDisplay";

    // Connected Bluetooth classic OR LE association endpoints.
    private const string ConnectedBtQuery =
        "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
        " System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\") AND " +
        "System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True";

    // Present device nodes (filtered down to the ones carrying a battery).
    private const string PresentDevicesQuery =
        "System.Devices.Present:=System.StructuredQueryType.Boolean#True";

    private static readonly string[] AepProps = { "System.Devices.Aep.IsConnected" };
    private static readonly string[] BatteryProps = { BatteryKey, NameKey };

    private readonly ConcurrentDictionary<string, (string Mac, string Name)> _connected = new();
    private readonly ConcurrentDictionary<string, int> _batteryNodes = new();

    private DeviceWatcher? _listWatcher;
    private DeviceWatcher? _batteryWatcher;

    /// <summary>Raised (on a background thread) with the current device list after any change.</summary>
    public event Action<IReadOnlyList<BtDevice>>? DevicesChanged;

    public void Start()
    {
        // Watcher 1 — the connected-device list.
        _listWatcher = DeviceInformation.CreateWatcher(
            ConnectedBtQuery, AepProps, DeviceInformationKind.AssociationEndpoint);

        _listWatcher.Added += (s, info) =>
        {
            var name = string.IsNullOrWhiteSpace(info.Name) ? "(unknown device)" : info.Name;
            _connected[info.Id] = (MacFromAep(info.Id), name);
            Publish();
        };
        _listWatcher.Removed += (s, update) =>
        {
            if (_connected.TryRemove(update.Id, out _)) Publish();
        };

        // Watcher 2 — battery values.
        _batteryWatcher = DeviceInformation.CreateWatcher(
            PresentDevicesQuery, BatteryProps, DeviceInformationKind.Device);

        _batteryWatcher.Added += (s, info) =>
        {
            if (ReadBattery(info.Properties) is int battery)
            {
                _batteryNodes[info.Id] = battery;
                Publish();
            }
        };
        _batteryWatcher.Updated += (s, update) =>
        {
            if (!update.Properties.ContainsKey(BatteryKey)) return;
            if (ReadBattery(update.Properties) is int battery) _batteryNodes[update.Id] = battery;
            else _batteryNodes.TryRemove(update.Id, out _);
            Publish();
        };
        _batteryWatcher.Removed += (s, update) =>
        {
            if (_batteryNodes.TryRemove(update.Id, out _)) Publish();
        };

        _listWatcher.Start();
        _batteryWatcher.Start();
    }

    public void Stop()
    {
        _listWatcher?.Stop();
        _batteryWatcher?.Stop();
    }

    /// <summary>Every connected device, with battery joined in by MAC, sorted by name.</summary>
    public IReadOnlyList<BtDevice> Snapshot() =>
        _connected.Select(kv => new BtDevice(kv.Key, kv.Value.Name, BatteryForMac(kv.Value.Mac)))
                  .OrderBy(d => d.Name)
                  .ToList();

    private int? BatteryForMac(string mac)
    {
        if (string.IsNullOrEmpty(mac)) return null;
        foreach (var (nodeId, percent) in _batteryNodes)
            if (nodeId.Contains(mac, StringComparison.OrdinalIgnoreCase))
                return percent;
        return null;
    }

    // AEP id ends with "...<adapterMac>-<deviceMac>"; take the hex after the last '-'.
    private static string MacFromAep(string id)
    {
        var dash = id.LastIndexOf('-');
        var tail = dash >= 0 ? id[(dash + 1)..] : id;
        return new string(tail.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
    }

    private static int? ReadBattery(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue(BatteryKey, out var value) && value is not null)
        {
            try { return Convert.ToInt32(value); } catch { /* unexpected type */ }
        }
        return null;
    }

    private void Publish() => DevicesChanged?.Invoke(Snapshot());
}
