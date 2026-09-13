using System.Buffers.Binary;
using System.Runtime.InteropServices;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class Kernel32WindowsProcessorApiTests
{
    [TestMethod]
    public void TopologyParserCountsOneAndMultiplePhysicalCoreRecords()
    {
        Assert.IsTrue(WindowsProcessorTopologyParser.TryCountPhysicalCores(CreateCoreRecords(1), out int one));
        Assert.AreEqual(1, one);
        Assert.IsTrue(WindowsProcessorTopologyParser.TryCountPhysicalCores(CreateCoreRecords(4), out int four));
        Assert.AreEqual(4, four);
    }

    [TestMethod]
    public void TopologyParserRejectsEmptyTruncatedNonProgressingAndTrailingData()
    {
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores([], out _));
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(new byte[7], out _));

        byte[] zeroSize = CreateCoreRecords(1);
        BinaryPrimitives.WriteUInt32LittleEndian(zeroSize.AsSpan(4, 4), 0);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(zeroSize, out _));

        byte[] tooSmall = CreateCoreRecords(1);
        BinaryPrimitives.WriteUInt32LittleEndian(tooSmall.AsSpan(4, 4), 7);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(tooSmall, out _));

        byte[] beyondBuffer = CreateCoreRecords(1);
        BinaryPrimitives.WriteUInt32LittleEndian(beyondBuffer.AsSpan(4, 4), 49);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(beyondBuffer, out _));

        byte[] trailing = new byte[49];
        CreateCoreRecords(1).CopyTo(trailing, 0);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(trailing, out _));
    }

    [TestMethod]
    public void TopologyParserRejectsUnsupportedRelationshipAndMoreThanMaximumCores()
    {
        byte[] unsupported = CreateCoreRecords(1);
        BinaryPrimitives.WriteInt32LittleEndian(unsupported.AsSpan(0, 4), 1);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(unsupported, out _));

        Assert.IsFalse(
            WindowsProcessorTopologyParser.TryCountPhysicalCores(CreateCoreRecords(4097), out _));
    }

    [TestMethod]
    public void TopologyParserRequiresExactlyOneGroupForEachCoreRecord()
    {
        byte[] zeroGroups = CreateCoreRecords(1);
        BinaryPrimitives.WriteUInt16LittleEndian(zeroGroups.AsSpan(30, 2), 0);
        Assert.IsFalse(WindowsProcessorTopologyParser.TryCountPhysicalCores(zeroGroups, out _));

        byte[] undersizedTwoGroups = CreateCoreRecords(1);
        BinaryPrimitives.WriteUInt16LittleEndian(undersizedTwoGroups.AsSpan(30, 2), 2);
        Assert.IsFalse(
            WindowsProcessorTopologyParser.TryCountPhysicalCores(undersizedTwoGroups, out _));

        byte[] sizedTwoGroups = CreateCoreRecord(groupCount: 2);
        Assert.IsFalse(
            WindowsProcessorTopologyParser.TryCountPhysicalCores(sizedTwoGroups, out _));
    }

    [TestMethod]
    [DataRow((ushort)0, (int)WindowsProcessorApiArchitecture.X86)]
    [DataRow((ushort)9, (int)WindowsProcessorApiArchitecture.X64)]
    [DataRow((ushort)12, (int)WindowsProcessorApiArchitecture.Arm64)]
    public void NativeAdapterMapsSupportedArchitectures(
        ushort nativeArchitecture,
        int expectedValue)
    {
        FakeProcessorNameSource names = new("Fixture Processor");
        FakeKernel32ProcessorNative native = new(CreateCoreRecords(2), nativeArchitecture, 4);

        WindowsProcessorApiResult result = new Kernel32WindowsProcessorApi(names, native).Capture();

        Assert.AreEqual(WindowsProcessorApiStatus.Success, result.Status);
        Assert.AreEqual((WindowsProcessorApiArchitecture)expectedValue, result.Architecture);
        Assert.AreEqual(2, result.PhysicalCoreCount);
        Assert.AreEqual(4, result.LogicalProcessorCount);
        Assert.AreEqual(2, native.QueryCalls);
        Assert.AreEqual(ushort.MaxValue, native.RequestedGroup);
    }

    [TestMethod]
    public void NativeAdapterRejectsUnsupportedArchitectureWithoutTopologyQuery()
    {
        FakeKernel32ProcessorNative native = new(CreateCoreRecords(2), 0xffff, 4);

        WindowsProcessorApiResult result = new Kernel32WindowsProcessorApi(
            new FakeProcessorNameSource("Fixture Processor"),
            native).Capture();

        Assert.AreEqual(WindowsProcessorApiStatus.UnsupportedArchitecture, result.Status);
        Assert.AreEqual(0, native.QueryCalls);
    }

    [TestMethod]
    public void NativeAdapterRejectsUnavailableNameBeforeNativeTopologyQuery()
    {
        FakeKernel32ProcessorNative native = new(CreateCoreRecords(2), 9, 4);

        WindowsProcessorApiResult result = new Kernel32WindowsProcessorApi(
            new FakeProcessorNameSource(null),
            native).Capture();

        Assert.AreEqual(WindowsProcessorApiStatus.NameUnavailable, result.Status);
        Assert.AreEqual(0, native.QueryCalls);
    }

    [TestMethod]
    public void NativeAdapterRejectsOversizedBufferAndInvalidLogicalCounts()
    {
        FakeKernel32ProcessorNative oversized = new(CreateCoreRecords(1), 9, 2)
        {
            FirstRequiredLength = (1024 * 1024) + 1,
        };
        Assert.AreEqual(
            WindowsProcessorApiStatus.TopologyUnavailable,
            new Kernel32WindowsProcessorApi(new FakeProcessorNameSource("Fixture"), oversized).Capture().Status);
        Assert.AreEqual(1, oversized.QueryCalls);

        FakeKernel32ProcessorNative zeroLogical = new(CreateCoreRecords(1), 9, 0);
        Assert.AreEqual(
            WindowsProcessorApiStatus.InvalidTopology,
            new Kernel32WindowsProcessorApi(new FakeProcessorNameSource("Fixture"), zeroLogical).Capture().Status);
    }

    [TestMethod]
    public void NativeAdapterMapsExpectedAvailabilityExceptionsToClosedStatus()
    {
        FakeKernel32ProcessorNative architectureFailure = new(CreateCoreRecords(1), 9, 2)
        {
            ArchitectureException = new DllNotFoundException("private native detail"),
        };
        FakeKernel32ProcessorNative queryFailure = new(CreateCoreRecords(1), 9, 2)
        {
            QueryException = new EntryPointNotFoundException("private native detail"),
        };
        FakeKernel32ProcessorNative activeCountFailure = new(CreateCoreRecords(1), 9, 2)
        {
            ActiveCountException = new PlatformNotSupportedException("private native detail"),
        };

        foreach (FakeKernel32ProcessorNative native in
            new[] { architectureFailure, queryFailure, activeCountFailure })
        {
            WindowsProcessorApiResult result = new Kernel32WindowsProcessorApi(
                new FakeProcessorNameSource("Fixture"),
                native).Capture();

            Assert.AreEqual(WindowsProcessorApiStatus.NativeApiUnavailable, result.Status);
            Assert.IsNull(result.Name);
        }
    }

    [TestMethod]
    public void RegistryNameSourceRejectsMissingWrongTypeUnsafeOversizedAndReadFailure()
    {
        Assert.IsFalse(new RegistryProcessorNameSource(new FakeRegistry(null)).TryGetName(out _));
        Assert.IsFalse(new RegistryProcessorNameSource(new FakeRegistry(42)).TryGetName(out _));
        Assert.IsFalse(new RegistryProcessorNameSource(new FakeRegistry(" unsafe")).TryGetName(out _));
        Assert.IsFalse(new RegistryProcessorNameSource(new FakeRegistry(new string('x', 257))).TryGetName(out _));
        Assert.IsFalse(new RegistryProcessorNameSource(new ThrowingRegistry()).TryGetName(out _));
    }

    [TestMethod]
    public void RegistryNameSourceReturnsValidatedTextWithoutChangingIt()
    {
        const string expected = "Fixture Cafe\u0301 Processor";

        Assert.IsTrue(new RegistryProcessorNameSource(new FakeRegistry(expected)).TryGetName(out string? actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void RegistryNameSourceRemovesWindowsBoundaryPaddingBeforeValidation()
    {
        const string observed = "AMD Ryzen 7 8845HS w/ Radeon 780M Graphics     ";
        const string expected = "AMD Ryzen 7 8845HS w/ Radeon 780M Graphics";

        Assert.IsTrue(new RegistryProcessorNameSource(new FakeRegistry(observed)).TryGetName(out string? actual));
        Assert.AreEqual(expected, actual);
    }

    private static byte[] CreateCoreRecords(int count)
    {
        const int recordSize = 48;
        byte[] buffer = new byte[checked(count * recordSize)];
        for (int index = 0; index < count; index++)
        {
            Span<byte> record = buffer.AsSpan(index * recordSize, recordSize);
            BinaryPrimitives.WriteInt32LittleEndian(record[..4], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(4, 4), recordSize);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(30, 2), 1);
        }

        return buffer;
    }

    private static byte[] CreateCoreRecord(ushort groupCount)
    {
        int recordSize = checked(32 + (groupCount * 16));
        byte[] record = new byte[recordSize];
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4, 4), checked((uint)recordSize));
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(30, 2), groupCount);
        return record;
    }

    private sealed class FakeProcessorNameSource(string? name) : IProcessorNameSource
    {
        public bool TryGetName(out string? value)
        {
            value = name;
            return name is not null;
        }
    }

    private sealed class FakeKernel32ProcessorNative(
        byte[] topology,
        ushort architecture,
        uint logicalProcessorCount) : IKernel32ProcessorNative
    {
        internal uint? FirstRequiredLength { get; init; }

        internal Exception? ArchitectureException { get; init; }

        internal Exception? QueryException { get; init; }

        internal Exception? ActiveCountException { get; init; }

        internal int QueryCalls { get; private set; }

        internal ushort RequestedGroup { get; private set; }

        public ushort GetNativeProcessorArchitecture() => ArchitectureException is null
            ? architecture
            : throw ArchitectureException;

        public uint GetActiveProcessorCount(ushort groupNumber)
        {
            if (ActiveCountException is not null)
            {
                throw ActiveCountException;
            }

            RequestedGroup = groupNumber;
            return logicalProcessorCount;
        }

        public ProcessorBufferQueryStatus QueryProcessorCoreInformation(IntPtr buffer, ref uint length)
        {
            if (QueryException is not null)
            {
                throw QueryException;
            }

            QueryCalls++;
            if (buffer == IntPtr.Zero)
            {
                length = FirstRequiredLength ?? checked((uint)topology.Length);
                return ProcessorBufferQueryStatus.BufferRequired;
            }

            if (length < topology.Length)
            {
                return ProcessorBufferQueryStatus.Failed;
            }

            Marshal.Copy(topology, 0, buffer, topology.Length);
            length = checked((uint)topology.Length);
            return ProcessorBufferQueryStatus.Success;
        }
    }

    private sealed class FakeRegistry(object? value) : IProcessorNameRegistry
    {
        public object? ReadProcessorName() => value;
    }

    private sealed class ThrowingRegistry : IProcessorNameRegistry
    {
        public object? ReadProcessorName() => throw new IOException("private registry failure");
    }
}
