using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using GuvenOn.Core.Protection;

namespace GuvenOn.Service.Protection;

/// <summary>
/// Applies the protection policy with a dynamic WFP session. Dynamic objects are
/// removed by Windows when the engine handle closes, so a service crash or a
/// panic reset cannot leave permanent firewall rules behind.
/// </summary>
internal sealed class WfpProtectionEngine : IDisposable
{
    private const byte WeightPermit = 15;
    private const byte WeightBlock = 5;
    private const byte WeightLegacyBlock = 4;

    private readonly ProtectionPolicy _policy;
    private IntPtr _engine;
    private Guid _providerKey;
    private Guid _subLayerKey;
    private bool _disposed;

    public WfpProtectionEngine(ProtectionPolicy policy)
    {
        _policy = policy;
    }

    public int FilterCount { get; private set; }

    public void Apply()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var layoutErrors = WfpInterop.ValidateLayouts();
        if (layoutErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "WFP structure layout validation failed: " + string.Join("; ", layoutErrors));
        }

        _providerKey = Guid.NewGuid();
        _subLayerKey = Guid.NewGuid();

        var session = BuildSession(out var sessionName, out var sessionDescription);
        uint status;
        try
        {
            status = WfpInterop.FwpmEngineOpen0(
                null,
                WfpInterop.RpcCAuthnWinnt,
                IntPtr.Zero,
                ref session,
                out _engine);
        }
        finally
        {
            Marshal.FreeHGlobal(sessionName);
            Marshal.FreeHGlobal(sessionDescription);
        }

        if (status != 0)
        {
            throw Failure("FwpmEngineOpen0", status);
        }

        var transactionStarted = false;
        try
        {
            status = WfpInterop.FwpmTransactionBegin0(_engine, 0);
            if (status != 0)
            {
                throw Failure("FwpmTransactionBegin0", status);
            }

            transactionStarted = true;
            AddProvider();
            AddSubLayer();
            AddFilters();

            status = WfpInterop.FwpmTransactionCommit0(_engine);
            if (status != 0)
            {
                throw Failure("FwpmTransactionCommit0", status);
            }

            transactionStarted = false;
        }
        catch
        {
            if (transactionStarted && _engine != IntPtr.Zero)
            {
                _ = WfpInterop.FwpmTransactionAbort0(_engine);
            }

            Dispose();
            throw;
        }
    }

    private void AddProvider()
    {
        var name = AllocString("Guven-ON");
        var description = AllocString("Guven-ON protection provider");
        try
        {
            var provider = new WfpInterop.FwpmProvider
            {
                ProviderKey = _providerKey,
                DisplayData = new WfpInterop.FwpmDisplayData
                {
                    Name = name,
                    Description = description
                }
            };

            var status = WfpInterop.FwpmProviderAdd0(_engine, ref provider, IntPtr.Zero);
            if (status != 0)
            {
                throw Failure("FwpmProviderAdd0", status);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(description);
        }
    }

    private void AddSubLayer()
    {
        var name = AllocString("Guven-ON filters");
        var description = AllocString("DNS and local-network protection");
        var providerKey = AllocGuid(_providerKey);
        try
        {
            var subLayer = new WfpInterop.FwpmSubLayer
            {
                SubLayerKey = _subLayerKey,
                DisplayData = new WfpInterop.FwpmDisplayData
                {
                    Name = name,
                    Description = description
                },
                ProviderKey = providerKey,
                Weight = ushort.MaxValue
            };

            var status = WfpInterop.FwpmSubLayerAdd0(_engine, ref subLayer, IntPtr.Zero);
            if (status != 0)
            {
                throw Failure("FwpmSubLayerAdd0", status);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(providerKey);
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(description);
        }
    }

    private void AddFilters()
    {
        AddLoopbackPermits();
        AddDhcpPermits();
        AddDnsRules();

        if (_policy.BlockLan)
        {
            AddLanBlocks();
        }

        if (_policy.BlockLegacyDiscovery)
        {
            AddLegacyDiscoveryBlocks();
        }
    }

    private void AddLoopbackPermits()
    {
        AddFilter(WfpInterop.LayerAleAuthConnectV4, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit loopback (IPv4 outbound)",
            builder => builder.AddFlags(WfpInterop.ConditionFlags, WfpInterop.FwpConditionFlagIsLoopback));

        AddFilter(WfpInterop.LayerAleAuthRecvAcceptV4, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit loopback (IPv4 inbound)",
            builder => builder.AddFlags(WfpInterop.ConditionFlags, WfpInterop.FwpConditionFlagIsLoopback));

        AddFilter(WfpInterop.LayerAleAuthConnectV6, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit loopback (IPv6 outbound)",
            builder => builder.AddFlags(WfpInterop.ConditionFlags, WfpInterop.FwpConditionFlagIsLoopback));

        AddFilter(WfpInterop.LayerAleAuthRecvAcceptV6, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit loopback (IPv6 inbound)",
            builder => builder.AddFlags(WfpInterop.ConditionFlags, WfpInterop.FwpConditionFlagIsLoopback));
    }

    private void AddDhcpPermits()
    {
        // Match only the protocol and port. Requiring the local port would make
        // the permit depend on the client socket already being bound to 68 when
        // WFP authorizes the flow, which is not guaranteed for every DHCP path.
        AddFilter(WfpInterop.LayerAleAuthConnectV4, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit DHCP request (IPv4)",
            builder =>
            {
                builder.AddUInt8(WfpInterop.ConditionIpProtocol, 17); // UDP
                builder.AddUInt16(WfpInterop.ConditionIpRemotePort, 67);
            });

        AddFilter(WfpInterop.LayerAleAuthRecvAcceptV4, WfpInterop.FwpActionPermit, WeightPermit,
            "Permit DHCP response (IPv4)",
            builder =>
            {
                builder.AddUInt8(WfpInterop.ConditionIpProtocol, 17);
                builder.AddUInt16(WfpInterop.ConditionIpRemotePort, 67);
            });
    }

    private void AddDnsRules()
    {
        var blockPorts = new List<ushort> { 53 };
        if (_policy.BlockDnsOverTls)
        {
            blockPorts.Add(853);
        }

        AddDnsBlock(WfpInterop.LayerAleAuthConnectV4, "Block DNS outbound (IPv4)", blockPorts);
        AddDnsBlock(WfpInterop.LayerAleAuthRecvAcceptV4, "Block DNS inbound (IPv4)", blockPorts);
        AddDnsBlock(WfpInterop.LayerAleAuthConnectV6, "Block DNS outbound (IPv6)", blockPorts);
        AddDnsBlock(WfpInterop.LayerAleAuthRecvAcceptV6, "Block DNS inbound (IPv6)", blockPorts);

        var v4Resolvers = _policy.DnsServersV4.ToArray();
        var v6Resolvers = _policy.DnsServersV6.ToArray();

        if (v4Resolvers.Length > 0)
        {
            AddDnsPermit(WfpInterop.LayerAleAuthConnectV4, "Permit trusted DNS outbound (IPv4)", blockPorts, v4Resolvers);
            AddDnsPermit(WfpInterop.LayerAleAuthRecvAcceptV4, "Permit trusted DNS inbound (IPv4)", blockPorts, v4Resolvers);
        }

        if (v6Resolvers.Length > 0)
        {
            AddDnsPermit(WfpInterop.LayerAleAuthConnectV6, "Permit trusted DNS outbound (IPv6)", blockPorts, v6Resolvers);
            AddDnsPermit(WfpInterop.LayerAleAuthRecvAcceptV6, "Permit trusted DNS inbound (IPv6)", blockPorts, v6Resolvers);
        }
    }

    private void AddDnsBlock(Guid layer, string name, IReadOnlyList<ushort> ports)
    {
        // WFP ANDs multiple conditions on the same field. A single filter with
        // remote-port 53 and 853 would therefore match nothing; each port needs
        // its own filter (and WFP ORs the resulting filters at the same weight).
        foreach (var port in ports)
        {
            var conditionPort = port;
            AddFilter(layer, WfpInterop.FwpActionBlock, WeightBlock, $"{name} port {port}", builder =>
                builder.AddUInt16(WfpInterop.ConditionIpRemotePort, conditionPort));
        }
    }

    private void AddDnsPermit(Guid layer, string name, IReadOnlyList<ushort> ports, IReadOnlyList<IPAddress> resolvers)
    {
        // Same rule: resolver addresses and ports are separate alternatives, so
        // emit one filter per address/port pair instead of one impossible AND.
        foreach (var resolver in resolvers)
        {
            foreach (var port in ports)
            {
                var conditionResolver = resolver;
                var conditionPort = port;
                AddFilter(
                    layer,
                    WfpInterop.FwpActionPermit,
                    WeightPermit,
                    $"{name} {conditionResolver}:{conditionPort}",
                    builder =>
                    {
                        builder.AddUInt16(WfpInterop.ConditionIpRemotePort, conditionPort);

                        if (conditionResolver.AddressFamily == AddressFamily.InterNetwork)
                        {
                            builder.AddUInt32(
                                WfpInterop.ConditionIpRemoteAddress,
                                ProtectionPolicy.ToHostOrderUInt32(conditionResolver));
                        }
                        else
                        {
                            builder.AddV6Address(WfpInterop.ConditionIpRemoteAddress, conditionResolver);
                        }
                    });
            }
        }
    }

    private void AddLanBlocks()
    {
        var v4Ranges = new[]
        {
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "169.254.0.0/16",
            "224.0.0.0/4",
            "255.255.255.255/32"
        };

        AddLanBlock(WfpInterop.LayerAleAuthConnectV4, "Block local network outbound (IPv4)", v4Ranges);
        AddLanBlock(WfpInterop.LayerAleAuthRecvAcceptV4, "Block local network inbound (IPv4)", v4Ranges);

        // ULA is local. Link-local IPv6 is intentionally not blocked because
        // neighbor discovery and DHCPv6 depend on it.
        AddLanBlock(WfpInterop.LayerAleAuthConnectV6, "Block local network outbound (IPv6)", new[] { "fc00::/7" });
        AddLanBlock(WfpInterop.LayerAleAuthRecvAcceptV6, "Block local network inbound (IPv6)", new[] { "fc00::/7" });
    }

    private void AddLanBlock(Guid layer, string name, IReadOnlyList<string> ranges)
    {
        // Multiple address ranges are alternatives, not an intersection.
        foreach (var range in ranges)
        {
            var conditionRange = range;
            AddFilter(layer, WfpInterop.FwpActionBlock, WeightBlock, $"{name} {range}", builder =>
            {
                if (conditionRange.Contains(':'))
                {
                    builder.AddV6Prefix(WfpInterop.ConditionIpRemoteAddress, conditionRange);
                }
                else
                {
                    builder.AddV4Prefix(WfpInterop.ConditionIpRemoteAddress, conditionRange);
                }
            });
        }
    }

    private void AddLegacyDiscoveryBlocks()
    {
        var ports = new ushort[] { 137, 138, 139, 445, 1900, 2869, 3702, 5353, 5355 };

        AddLegacyBlock(WfpInterop.LayerAleAuthConnectV4, "Block legacy discovery outbound (IPv4)", ports);
        AddLegacyBlock(WfpInterop.LayerAleAuthRecvAcceptV4, "Block legacy discovery inbound (IPv4)", ports);
        AddLegacyBlock(WfpInterop.LayerAleAuthConnectV6, "Block legacy discovery outbound (IPv6)", ports);
        AddLegacyBlock(WfpInterop.LayerAleAuthRecvAcceptV6, "Block legacy discovery inbound (IPv6)", ports);
    }

    private void AddLegacyBlock(Guid layer, string name, IReadOnlyList<ushort> ports)
    {
        foreach (var port in ports)
        {
            var conditionPort = port;
            AddFilter(layer, WfpInterop.FwpActionBlock, WeightLegacyBlock, $"{name} port {port}", builder =>
                builder.AddUInt16(WfpInterop.ConditionIpRemotePort, conditionPort));
        }
    }

    private void AddFilter(Guid layer, uint action, byte weight, string name, Action<FilterBuilder> configure)
    {
        using var builder = new FilterBuilder();
        configure(builder);

        var filter = builder.Build(layer, _providerKey, _subLayerKey, action, weight, name);
        var status = WfpInterop.FwpmFilterAdd0(_engine, ref filter, IntPtr.Zero, out _);
        if (status != 0)
        {
            throw Failure($"FwpmFilterAdd0 ({name})", status);
        }

        FilterCount++;
    }

    private static WfpInterop.FwpmSession BuildSession(out IntPtr name, out IntPtr description)
    {
        name = AllocString("Guven-ON");
        description = AllocString("Guven-ON dynamic protection session");

        return new WfpInterop.FwpmSession
        {
            DisplayData = new WfpInterop.FwpmDisplayData
            {
                Name = name,
                Description = description
            },
            Flags = WfpInterop.FwpmSessionFlagDynamic,
            TxnWaitTimeoutInMSec = WfpInterop.Infinite
        };
    }

    private static IntPtr AllocString(string value) => Marshal.StringToHGlobalUni(value);

    private static IntPtr AllocGuid(Guid value)
    {
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        Marshal.StructureToPtr(value, pointer, false);
        return pointer;
    }

    private static Win32Exception Failure(string operation, uint status) =>
        new((int)status, $"{operation} failed (0x{status:X8}).");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FilterCount = 0;

        if (_engine != IntPtr.Zero)
        {
            _ = WfpInterop.FwpmEngineClose0(_engine);
            _engine = IntPtr.Zero;
        }
    }

    private sealed class FilterBuilder : IDisposable
    {
        private readonly List<WfpInterop.FwpmFilterCondition> _conditions = new();
        private readonly List<IntPtr> _allocations = new();

        public void AddUInt8(Guid field, byte value) =>
            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.UInt8,
                Value = new WfpInterop.FwpValueUnion { UInt8 = value }
            });

        public void AddUInt16(Guid field, ushort value) =>
            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.UInt16,
                Value = new WfpInterop.FwpValueUnion { UInt16 = value }
            });

        public void AddUInt32(Guid field, uint value) =>
            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.UInt32,
                Value = new WfpInterop.FwpValueUnion { UInt32 = value }
            });

        public void AddFlags(Guid field, uint value) =>
            Add(field, WfpInterop.FwpMatchType.FlagsAllSet, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.UInt32,
                Value = new WfpInterop.FwpValueUnion { UInt32 = value }
            });

        public void AddV4Prefix(Guid field, string cidr)
        {
            var (address, prefix) = ParseCidr(cidr, AddressFamily.InterNetwork);
            var value = new WfpInterop.FwpV4AddrAndMask
            {
                Address = ProtectionPolicy.ToHostOrderUInt32(address),
                Mask = ProtectionPolicy.NetmaskFromPrefix(prefix)
            };

            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<WfpInterop.FwpV4AddrAndMask>());
            Marshal.StructureToPtr(value, pointer, false);
            _allocations.Add(pointer);

            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.V4AddrMask,
                Value = new WfpInterop.FwpValueUnion { Pointer = pointer }
            });
        }

        public void AddV6Prefix(Guid field, string cidr)
        {
            var (address, prefix) = ParseCidr(cidr, AddressFamily.InterNetworkV6);
            var pointer = Marshal.AllocHGlobal(17);
            _allocations.Add(pointer);

            Marshal.Copy(address.GetAddressBytes(), 0, pointer, 16);
            Marshal.WriteByte(pointer, 16, (byte)prefix);

            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.V6AddrMask,
                Value = new WfpInterop.FwpValueUnion { Pointer = pointer }
            });
        }

        public void AddV6Address(Guid field, IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ArgumentException("An IPv6 address is required.", nameof(address));
            }

            var pointer = Marshal.AllocHGlobal(16);
            _allocations.Add(pointer);
            Marshal.Copy(address.GetAddressBytes(), 0, pointer, 16);

            Add(field, WfpInterop.FwpMatchType.Equal, new WfpInterop.FwpConditionValue
            {
                Type = WfpInterop.FwpDataType.ByteArray16,
                Value = new WfpInterop.FwpValueUnion { Pointer = pointer }
            });
        }

        public WfpInterop.FwpmFilter Build(
            Guid layer,
            Guid providerKey,
            Guid subLayerKey,
            uint action,
            byte weight,
            string name)
        {
            var providerKeyPointer = AllocGuid(providerKey);
            var namePointer = Marshal.StringToHGlobalUni(name);
            var descriptionPointer = Marshal.StringToHGlobalUni(string.Empty);
            _allocations.Add(providerKeyPointer);
            _allocations.Add(namePointer);
            _allocations.Add(descriptionPointer);

            var conditionSize = Marshal.SizeOf<WfpInterop.FwpmFilterCondition>();
            var conditionsPointer = Marshal.AllocHGlobal(conditionSize * Math.Max(1, _conditions.Count));
            _allocations.Add(conditionsPointer);

            for (var index = 0; index < _conditions.Count; index++)
            {
                Marshal.StructureToPtr(
                    _conditions[index],
                    conditionsPointer + (index * conditionSize),
                    false);
            }

            return new WfpInterop.FwpmFilter
            {
                DisplayData = new WfpInterop.FwpmDisplayData
                {
                    Name = namePointer,
                    Description = descriptionPointer
                },
                ProviderKey = providerKeyPointer,
                LayerKey = layer,
                SubLayerKey = subLayerKey,
                Weight = new WfpInterop.FwpValue
                {
                    Type = WfpInterop.FwpDataType.UInt8,
                    Value = new WfpInterop.FwpValueUnion { UInt8 = weight }
                },
                NumFilterConditions = (uint)_conditions.Count,
                FilterConditions = conditionsPointer,
                Action = new WfpInterop.FwpmAction
                {
                    Type = action,
                    FilterType = Guid.Empty
                }
            };
        }

        private void Add(Guid field, WfpInterop.FwpMatchType match, WfpInterop.FwpConditionValue value) =>
            _conditions.Add(new WfpInterop.FwpmFilterCondition
            {
                FieldKey = field,
                MatchType = match,
                ConditionValue = value
            });

        private static (IPAddress Address, int Prefix) ParseCidr(string cidr, AddressFamily family)
        {
            var parts = cidr.Split('/', 2);
            if (parts.Length != 2 ||
                !IPAddress.TryParse(parts[0], out var address) ||
                address.AddressFamily != family ||
                !int.TryParse(parts[1], out var prefix))
            {
                throw new ArgumentException($"Invalid CIDR range: {cidr}", nameof(cidr));
            }

            var maxPrefix = family == AddressFamily.InterNetwork ? 32 : 128;
            if (prefix is < 0 || prefix > maxPrefix)
            {
                throw new ArgumentOutOfRangeException(nameof(cidr), $"Invalid prefix length in {cidr}.");
            }

            return (address, prefix);
        }

        public void Dispose()
        {
            foreach (var pointer in _allocations)
            {
                Marshal.FreeHGlobal(pointer);
            }

            _allocations.Clear();
            _conditions.Clear();
        }
    }
}
