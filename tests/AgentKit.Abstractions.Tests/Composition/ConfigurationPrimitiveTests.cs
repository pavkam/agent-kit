// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

public sealed class ConfigurationPrimitiveTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextValueBlank_ThrowsArgumentExceptionWithValue(string value)
    {
        Should.Throw<ArgumentException>(() => new ConfigurationSourceId(value)).ParamName.ShouldBe("value");
        Should.Throw<ArgumentException>(() => new ConfigurationPath(value)).ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenTextValueNull_ThrowsArgumentNullExceptionWithValue()
    {
        Should.Throw<ArgumentNullException>(() => new ConfigurationSourceId(null!)).ParamName.ShouldBe("value");
        Should.Throw<ArgumentNullException>(() => new ConfigurationPath(null!)).ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenTextValueValid_PreservesOrdinalValueAndEquality()
    {
        new ConfigurationSourceId("A").ShouldBe(new ConfigurationSourceId("A"));
        new ConfigurationSourceId("A").ShouldNotBe(new ConfigurationSourceId("a"));
        new ConfigurationPath("Agent.Option").ToString().ShouldBe("Agent.Option");
        new ConfigurationPath("Agent.Option").ShouldNotBe(new ConfigurationPath("agent.option"));
        default(ConfigurationSourceId).ToString().ShouldBe(string.Empty);
        default(ConfigurationPath).ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenSourceVersionNotPositive_ThrowsArgumentOutOfRangeWithValue(long value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationSourceVersion(value)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenSourceVersionPositive_PreservesBoundary() => new ConfigurationSourceVersion(long.MaxValue).Value.ShouldBe(long.MaxValue);

    [Theory]
    [InlineData(99)]
    public void Constructor_WhenSourceReferenceEnumUndefined_ThrowsArgumentOutOfRange(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Reference((ConfigurationLayerKind) value, ConfigurationTrustClass.Untrusted)).ParamName.ShouldBe("layer");
        Should.Throw<ArgumentOutOfRangeException>(() => Reference(ConfigurationLayerKind.HostGlobal, (ConfigurationTrustClass) value)).ParamName.ShouldBe("trust");
    }

    [Fact]
    public void Constructor_WhenSourceReferenceIdentityDefault_ThrowsArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationSourceReference(default, new ConfigurationSourceVersion(1), ConfigurationLayerKind.HostGlobal, ConfigurationTrustClass.HostEstablished, new ContentHash("h"))).ParamName.ShouldBe("sourceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationSourceReference(new ConfigurationSourceId("s"), default, ConfigurationLayerKind.HostGlobal, ConfigurationTrustClass.HostEstablished, new ContentHash("h"))).ParamName.ShouldBe("sourceVersion");
        Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationSourceReference(new ConfigurationSourceId("s"), new ConfigurationSourceVersion(1), ConfigurationLayerKind.HostGlobal, ConfigurationTrustClass.HostEstablished, default)).ParamName.ShouldBe("fingerprint");
    }

    [Fact]
    public void Constructor_WhenSourceReferenceValid_CapturesStructurallyEqualImmutableEvidence()
    {
        var first = Reference(ConfigurationLayerKind.RunInvocation, ConfigurationTrustClass.HostEstablished);
        var second = Reference(ConfigurationLayerKind.RunInvocation, ConfigurationTrustClass.HostEstablished);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.SourceId.ShouldBe(new ConfigurationSourceId("host"));
        first.SourceVersion.ShouldBe(new ConfigurationSourceVersion(1));
        first.Layer.ShouldBe(ConfigurationLayerKind.RunInvocation);
        first.Trust.ShouldBe(ConfigurationTrustClass.HostEstablished);
        first.Fingerprint.ShouldBe(new ContentHash("sha256:configuration"));
        (first with { }).ShouldBe(first);
    }

    private static ConfigurationSourceReference Reference(ConfigurationLayerKind layer, ConfigurationTrustClass trust) =>
        new(new ConfigurationSourceId("host"), new ConfigurationSourceVersion(1), layer, trust, new ContentHash("sha256:configuration"));
}
