// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies EffectiveConfigurationEntry behavior and contracts.</summary>
public sealed class EffectiveConfigurationEntryTests
{
    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenContributorsEmpty_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.Replace, JsonValue(), []));
        exception.ParamName.ShouldBe("contributors");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenPathDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EffectiveConfigurationEntry(default, ConfigurationMergeOperation.Replace, JsonValue(), [Source("host", 1)]));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenContributorsDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.Replace, JsonValue(), default));
        exception.ParamName.ShouldBe("contributors");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenContributorNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.Replace, JsonValue(), [null!]));
        exception.ParamName.ShouldBe("contributors");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenValueNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.Replace, null!, [Source("host", 1)]));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenMergeUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), (ConfigurationMergeOperation) 99, JsonValue(), [Source("host", 1)]));
        exception.ParamName.ShouldBe("mergeOperation");
    }

    [Fact]
    public void EffectiveConfigurationEntry_Constructor_WhenContributorSourceRepeats_ThrowsExactException()
    {
        var source = Source("host", 1);
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.DeepMerge, JsonValue(), [source, source]));
        exception.ParamName.ShouldBe("contributors");
    }

    private static ConfigurationSourceReference Source(string id, long version) => new(new ConfigurationSourceId(id), new ConfigurationSourceVersion(version), ConfigurationLayerKind.HostGlobal, ConfigurationTrustClass.HostEstablished, new ContentHash($"hash-{id}-{version}"));
    private static ConfigurationJsonValue JsonValue()
    {
        using var document = JsonDocument.Parse("true");
        return new ConfigurationJsonValue(document.RootElement);
    }
}
