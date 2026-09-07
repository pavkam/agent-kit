// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using System.Globalization;

using AgentKit;

/// <summary>
/// Exercises the constructor guards, canonical text, and equality of the
/// durability identity and key value types.
/// </summary>
public sealed class DurabilityIdentityTests
{
    public static TheoryData<Func<Guid, object>> GuidIdentityFactories() =>
        new()
        {
            value => new CheckpointId(value),
            value => new ExecutionLeaseId(value),
            value => new WorkerId(value),
        };

    public static TheoryData<Func<string, object>> TextKeyFactories() =>
        new()
        {
            value => new DurableOperationName(value),
            value => new DurableOperationVersion(value),
            value => new DurabilityProfileKey(value),
            value => new DurableBackendKey(value),
            value => new DurableJournalKey(value),
            value => new DurableLeaseManagerKey(value),
            value => new RecoveryPolicyKey(value),
        };

    [Theory]
    [MemberData(nameof(GuidIdentityFactories))]
    public void GuidIdentity_Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException(
        Func<Guid, object> factory)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => factory(Guid.Empty));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(GuidIdentityFactories))]
    public void GuidIdentity_Constructor_WhenValueIsPopulated_Succeeds(Func<Guid, object> factory)
    {
        var identity = factory(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        identity.ToString().ShouldBe("11111111-2222-3333-4444-555555555555");
    }

    [Theory]
    [MemberData(nameof(TextKeyFactories))]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException(
        Func<string, object> factory)
    {
        var exception = Should.Throw<ArgumentException>(() => factory(null!));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(TextKeyFactories))]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException(
        Func<string, object> factory)
    {
        var exception = Should.Throw<ArgumentException>(() => factory(string.Empty));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(TextKeyFactories))]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException(
        Func<string, object> factory)
    {
        var exception = Should.Throw<ArgumentException>(() => factory("   "));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(TextKeyFactories))]
    public void TextKey_ToString_ReturnsCanonicalText(Func<string, object> factory) =>
        factory("canonical").ToString().ShouldBe("canonical");

    [Fact]
    public void DurabilityProfileVersion_Constructor_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurabilityProfileVersion(-1));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void DurabilityProfileVersion_Constructor_WhenValueIsZero_Succeeds() =>
        new DurabilityProfileVersion(0).Value.ShouldBe(0);

    [Fact]
    public void DurabilityProfileVersion_ToString_ReturnsInvariantCultureText() =>
        new DurabilityProfileVersion(12).ToString()
            .ShouldBe(12.ToString(CultureInfo.InvariantCulture));

    [Fact]
    public void DurableOperationName_Equality_WhenSameText_InstancesAreEqual() =>
        new DurableOperationName("tool.call").ShouldBe(new DurableOperationName("tool.call"));

    [Fact]
    public void DurableOperationName_Equality_WhenDifferentCase_InstancesAreNotEqual() =>
        new DurableOperationName("tool.call").ShouldNotBe(new DurableOperationName("Tool.Call"));

    [Fact]
    public void DurableJournalKey_Equality_WhenSameTextAsBackendKey_TypesRemainDistinct()
    {
        object journal = new DurableJournalKey("shared");
        object backend = new DurableBackendKey("shared");

        journal.ShouldNotBe(backend);
    }
}
