using System.Runtime.InteropServices;

namespace GuvenOn.Service.Protection;

/// <summary>
/// Minimal, explicit WFP interop. The layouts mirror the Windows SDK headers
/// (x64) and are validated before any filter is added.
/// </summary>
internal static class WfpInterop
{
    public const uint RpcCAuthnWinnt = 10;
    public const uint FwpmSessionFlagDynamic = 0x00000001;
    public const uint FwpmProviderFlagPersistent = 0x00000001;
    public const uint FwpmSubLayerFlagPersistent = 0x00000001;
    public const uint FwpmFilterFlagPersistent = 0x00000001;
    public const uint FwpActionFlagTerminating = 0x00001000;
    public const uint FwpActionBlock = 0x00000001 | FwpActionFlagTerminating;
    public const uint FwpActionPermit = 0x00000002 | FwpActionFlagTerminating;
    public const uint FwpConditionFlagIsLoopback = 0x00000001;
    public const uint Infinite = 0xFFFFFFFF;

    public const uint FwpEFilterNotFound = 0x80320003;
    public const uint FwpEProviderNotFound = 0x80320005;
    public const uint FwpESubLayerNotFound = 0x80320007;

    public static readonly Guid LayerAleAuthConnectV4 =
        new("c38d57d1-05a7-4c33-904f-7fbceee60e82");

    public static readonly Guid LayerAleAuthConnectV6 =
        new("4a72393b-319f-44bc-84c3-ba54dcb3b6b4");

    public static readonly Guid LayerAleAuthRecvAcceptV4 =
        new("e1cd9fe7-f4b5-4273-96c0-592e487b8650");

    public static readonly Guid LayerAleAuthRecvAcceptV6 =
        new("a3b42c97-9f04-4672-b87e-cee9c483257f");

    public static readonly Guid ConditionIpProtocol =
        new("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7");

    public static readonly Guid ConditionIpLocalPort =
        new("0c1ba1af-5765-453f-af22-a8f791ac775b");

    public static readonly Guid ConditionIpRemotePort =
        new("c35a604d-d22b-4e1a-91b4-68f674ee674b");

    public static readonly Guid ConditionIpRemoteAddress =
        new("b235ae9a-1d64-49b8-a44c-5ff3d9095045");

    public static readonly Guid ConditionFlags =
        new("632ce23b-5167-435c-86d7-e903684aa80c");

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmEngineOpen0(
        [MarshalAs(UnmanagedType.LPWStr)] string? serverName,
        uint authnService,
        IntPtr authIdentity,
        ref FwpmSession session,
        out IntPtr engineHandle);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmEngineClose0(IntPtr engineHandle);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmTransactionBegin0(IntPtr engineHandle, uint flags);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmTransactionCommit0(IntPtr engineHandle);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmTransactionAbort0(IntPtr engineHandle);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmProviderAdd0(
        IntPtr engineHandle,
        ref FwpmProvider provider,
        IntPtr securityDescriptor);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmSubLayerAdd0(
        IntPtr engineHandle,
        ref FwpmSubLayer subLayer,
        IntPtr securityDescriptor);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmFilterAdd0(
        IntPtr engineHandle,
        ref FwpmFilter filter,
        IntPtr securityDescriptor,
        out ulong filterId);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmFilterDeleteByKey0(IntPtr engineHandle, in Guid key);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmSubLayerDeleteByKey0(IntPtr engineHandle, in Guid key);

    [DllImport("fwpuclnt.dll", ExactSpelling = true)]
    public static extern uint FwpmProviderDeleteByKey0(IntPtr engineHandle, in Guid key);

    public static IReadOnlyList<string> ValidateLayouts()
    {
        var mismatches = new List<string>();

        Check<FwpmSession>(72, mismatches);
        Check<FwpmProvider>(64, mismatches);
        Check<FwpmSubLayer>(72, mismatches);
        Check<FwpmFilter>(192, mismatches);
        Check<FwpmFilterCondition>(40, mismatches);
        Check<FwpmAction>(24, mismatches);
        Check<FwpValue>(16, mismatches);
        Check<FwpConditionValue>(16, mismatches);
        Check<FwpV4AddrAndMask>(8, mismatches);

        CheckOffset<FwpmAction>(nameof(FwpmAction.FilterType), 8, mismatches);
        CheckOffset<FwpmFilterCondition>(nameof(FwpmFilterCondition.ConditionValue), 24, mismatches);
        CheckOffset<FwpmFilter>(nameof(FwpmFilter.ProviderKey), 40, mismatches);
        CheckOffset<FwpmFilter>(nameof(FwpmFilter.FilterConditions), 120, mismatches);
        CheckOffset<FwpmFilter>(nameof(FwpmFilter.Action), 128, mismatches);
        CheckOffset<FwpmFilter>(nameof(FwpmFilter.RawContext), 152, mismatches);

        return mismatches;
    }

    private static void Check<T>(int expected, List<string> mismatches) where T : struct
    {
        var actual = Marshal.SizeOf<T>();
        if (actual != expected)
        {
            mismatches.Add($"{typeof(T).Name}: expected {expected} bytes, got {actual}");
        }
    }

    private static void CheckOffset<T>(string field, int expected, List<string> mismatches) where T : struct
    {
        var actual = (int)Marshal.OffsetOf<T>(field);
        if (actual != expected)
        {
            mismatches.Add($"{typeof(T).Name}.{field}: expected offset {expected}, got {actual}");
        }
    }

    public enum FwpDataType : uint
    {
        Empty = 0,
        UInt8 = 1,
        UInt16 = 2,
        UInt32 = 3,
        UInt64 = 4,
        ByteArray16 = 11,
        V4AddrMask = 0x100,
        V6AddrMask = 0x101
    }

    public enum FwpMatchType : uint
    {
        Equal = 0,
        Greater = 1,
        Less = 2,
        GreaterOrEqual = 3,
        LessOrEqual = 4,
        Range = 5,
        FlagsAllSet = 6,
        FlagsAnySet = 7,
        FlagsNoneSet = 8,
        EqualCaseInsensitive = 9,
        NotEqual = 10,
        Prefix = 11,
        NotPrefix = 12
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmDisplayData
    {
        public IntPtr Name;
        public IntPtr Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpByteBlob
    {
        public uint Size;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct FwpValueUnion
    {
        [FieldOffset(0)] public byte UInt8;
        [FieldOffset(0)] public ushort UInt16;
        [FieldOffset(0)] public uint UInt32;
        [FieldOffset(0)] public ulong UInt64;
        [FieldOffset(0)] public IntPtr Pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpValue
    {
        public FwpDataType Type;
        public FwpValueUnion Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpConditionValue
    {
        public FwpDataType Type;
        public FwpValueUnion Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpV4AddrAndMask
    {
        public uint Address;
        public uint Mask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmFilterCondition
    {
        public Guid FieldKey;
        public FwpMatchType MatchType;
        public FwpConditionValue ConditionValue;
    }

    // The native FWPM_ACTION0 union contains both a GUID and a UINT64 callout
    // key, so it is 8-byte aligned and starts at offset 8. A plain sequential
    // struct would put FilterType at offset 4 and corrupt the filter.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct FwpmAction
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public Guid FilterType;
        [FieldOffset(8)] public ulong CalloutKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmFilter
    {
        public Guid FilterKey;
        public FwpmDisplayData DisplayData;
        public uint Flags;
        public IntPtr ProviderKey;
        public FwpByteBlob ProviderData;
        public Guid LayerKey;
        public Guid SubLayerKey;
        public FwpValue Weight;
        public uint NumFilterConditions;
        public IntPtr FilterConditions;
        public FwpmAction Action;
        public ulong RawContext;
        public IntPtr Reserved;
        public ulong FilterId;
        public FwpValue EffectiveWeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmProvider
    {
        public Guid ProviderKey;
        public FwpmDisplayData DisplayData;
        public uint Flags;
        public FwpByteBlob ProviderData;
        public IntPtr ServiceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmSubLayer
    {
        public Guid SubLayerKey;
        public FwpmDisplayData DisplayData;
        public uint Flags;
        public IntPtr ProviderKey;
        public FwpByteBlob ProviderData;
        public ushort Weight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FwpmSession
    {
        public Guid SessionKey;
        public FwpmDisplayData DisplayData;
        public uint Flags;
        public uint TxnWaitTimeoutInMSec;
        public uint ProcessId;
        public IntPtr Sid;
        public IntPtr Username;
        public int KernelMode;
    }
}
