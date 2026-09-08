// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

public sealed class SessionEntryCodecContractsTests
{
    [Fact]
    public void SessionEntryTypeId_Constructor_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryTypeId(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SessionEntryTypeId_Constructor_WhenValueIsBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryTypeId(value));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void SessionEntryTypeId_DefaultValue_HasNoWireIdentityAndFormatsAsEmpty()
    {
        var typeId = default(SessionEntryTypeId);

        typeId.Value.ShouldBeNull();
        typeId.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void SessionEntryTypeId_Constructor_WhenValueIsValid_PreservesStableWireIdentity()
    {
        var typeId = new SessionEntryTypeId("agentkit.session.message");

        typeId.Value.ShouldBe("agentkit.session.message");
        typeId.ToString().ShouldBe(typeId.Value);
        typeId.ShouldBe(new SessionEntryTypeId("agentkit.session.message"));
    }

    [Theory]
    [InlineData(0, 1, 1, 1, "maximumPayloadBytes")]
    [InlineData(-1, 1, 1, 1, "maximumPayloadBytes")]
    [InlineData(1, 0, 1, 1, "maximumExtensionCount")]
    [InlineData(1, -1, 1, 1, "maximumExtensionCount")]
    [InlineData(1, 1, 0, 1, "maximumExtensionBytes")]
    [InlineData(1, 1, -1, 1, "maximumExtensionBytes")]
    [InlineData(1, 1, 1, 0, "maximumJsonDepth")]
    [InlineData(1, 1, 1, -1, "maximumJsonDepth")]
    public void SessionEntryCodecLimits_Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException(
        int maximumPayloadBytes,
        int maximumExtensionCount,
        int maximumExtensionBytes,
        int maximumJsonDepth,
        string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecLimits(
            maximumPayloadBytes,
            maximumExtensionCount,
            maximumExtensionBytes,
            maximumJsonDepth));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void SessionEntryCodecLimits_Constructor_WhenLimitsArePositive_PreservesEveryFiniteBound()
    {
        var limits = new SessionEntryCodecLimits(int.MaxValue, 1, int.MaxValue, 64);

        limits.MaximumPayloadBytes.ShouldBe(int.MaxValue);
        limits.MaximumExtensionCount.ShouldBe(1);
        limits.MaximumExtensionBytes.ShouldBe(int.MaxValue);
        limits.MaximumJsonDepth.ShouldBe(64);
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenTypeIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecDescriptor(
            default,
            typeof(MessageSessionEntry),
            Version1,
            [Version1],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("typeId");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenEntryTypeIsNull_ThrowsArgumentNullException()
    {
        Type entryType = null!;

        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            entryType,
            Version1,
            [Version1],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("entryType");
    }

    [Theory]
    [InlineData(typeof(SessionEntry))]
    [InlineData(typeof(List<>))]
    [InlineData(typeof(string))]
    public void SessionEntryCodecDescriptor_Constructor_WhenEntryTypeIsNotConcreteClosedSessionEntry_ThrowsArgumentException(Type entryType)
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            entryType,
            Version1,
            [Version1],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("entryType");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenWriteVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            default,
            [Version1],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("writeVersion");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenReadableVersionsAreDefault_ThrowsArgumentException()
    {
        ImmutableArray<SchemaVersion> readableVersions = default;

        var exception = Should.Throw<ArgumentException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            readableVersions,
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenReadableVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [Version1, default],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenReadableVersionsAreEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenReadableVersionsContainDuplicate_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [Version1, Version1],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenReadableVersionsOmitWriteVersion_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [Version2],
            Limits()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenLimitsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [Version1],
            null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Constructor_WhenDeclarationIsValid_PreservesExactDispatchMetadata()
    {
        var descriptor = Descriptor([Version1, Version2]);

        descriptor.TypeId.ShouldBe(TypeId);
        descriptor.EntryType.ShouldBe(typeof(MessageSessionEntry));
        descriptor.WriteVersion.ShouldBe(Version1);
        descriptor.ReadableVersions.ShouldBe([Version1, Version2]);
        descriptor.Limits.ShouldBe(Limits());
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Equality_WhenReadableVersionsHaveSameOrderedValues_IsStructural()
    {
        var first = Descriptor([Version1, Version2]);
        var second = Descriptor([Version1, Version2]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Equality_WhenScalarMembersDifferOrOtherIsNull_IsNotEqual()
    {
        var descriptor = Descriptor([Version1, Version2]);

        descriptor.Equals(null).ShouldBeFalse();
        descriptor.ShouldNotBe(new SessionEntryCodecDescriptor(
            new SessionEntryTypeId("agentkit.session.other"),
            typeof(MessageSessionEntry),
            Version1,
            [Version1, Version2],
            Limits()));
        descriptor.ShouldNotBe(new SessionEntryCodecDescriptor(
            TypeId,
            typeof(InputAdmittedSessionEntry),
            Version1,
            [Version1, Version2],
            Limits()));
        descriptor.ShouldNotBe(new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version2,
            [Version1, Version2],
            Limits()));
        descriptor.ShouldNotBe(new SessionEntryCodecDescriptor(
            TypeId,
            typeof(MessageSessionEntry),
            Version1,
            [Version1, Version2],
            new SessionEntryCodecLimits(2048, 8, 512, 16)));
    }

    [Fact]
    public void SessionEntryCodecDescriptor_Equality_WhenReadableVersionsHaveDifferentOrder_IsNotEqual()
    {
        var first = Descriptor([Version1, Version2]);
        var second = Descriptor([Version2, Version1]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenValuesAreValid_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(
            [Version1, Version2],
            Version1));

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenWriteVersionIsDefault_ReportsWriteVersionParameterName()
    {
        SchemaVersion writeVersion = default;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1], writeVersion));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("writeVersion");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenVersionsAreEmpty_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = [];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenVersionsAreDefault_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenAValueIsDefault_ReportsExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1, default], Version1, "versions"));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("versions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenAValueIsDuplicate_ReportsExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1, Version1], Version1, "versions"));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("versions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenWriteVersionIsOmitted_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = [Version2];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    private static readonly SessionEntryTypeId TypeId = new("agentkit.session.message");
    private static readonly SchemaVersion Version1 = new("1");
    private static readonly SchemaVersion Version2 = new("2");

    private static SessionEntryCodecDescriptor Descriptor(ImmutableArray<SchemaVersion> readableVersions) => new(
        TypeId,
        typeof(MessageSessionEntry),
        Version1,
        readableVersions,
        Limits());

    private static SessionEntryCodecLimits Limits() => new(1024, 8, 512, 16);
}
