// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies EffectiveConfigurationSnapshot behavior and contracts.</summary>
public sealed class EffectiveConfigurationSnapshotTests
{
    [Fact]
    public void Constructor_WhenVersionDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EffectiveConfigurationSnapshot(default, new ContentHash("hash"), [], []));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenEntriesDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("hash"), default, []));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenSourcesDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("hash"), [], default));
        exception.ParamName.ShouldBe("sources");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenArrayContainsNull_ThrowsExactException(bool entryArray)
    {
        var exception = entryArray ? Should.Throw<ArgumentException>(() => Snapshot([null!], [])) : Should.Throw<ArgumentException>(() => Snapshot([], [null!]));
        exception.ParamName.ShouldBe(entryArray ? "entries" : "sources");
    }

    [Fact]
    public void Constructor_WhenFingerprintDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), default, [], []));
        exception.ParamName.ShouldBe("fingerprint");
    }

    [Fact]
    public void Constructor_WhenEmpty_RetainsLocallyValidEmptySnapshot()
    {
        var snapshot = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("empty"), [], []);
        snapshot.Entries.ShouldBeEmpty();
        snapshot.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenPathsNotStrictlyOrdinal_ThrowsExactException()
    {
        var source = Source("host", 1);
        var first = Entry("z.value", source);
        var second = Entry("a.value", source);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([first, second], [source]));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenPathsDuplicate_ThrowsExactException()
    {
        var source = Source("host", 1);
        var first = Entry("agent.value", source);
        var duplicate = Entry("agent.value", source);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([first, duplicate], [source]));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenContributorMissingFromSources_ThrowsExactException()
    {
        var source = Source("host", 1);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([Entry("agent.value", source)], []));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenContributorRevisionDiffersFromClosure_ThrowsExactException()
    {
        var published = Source("host", 1);
        var mismatched = Source("host", 2);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([Entry("agent.value", mismatched)], [published]));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenSourceDoesNotParticipate_ThrowsExactException()
    {
        var participating = Source("host", 1);
        var unused = Source("application", 1);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([Entry("agent.value", participating)], [participating, unused]));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenSourceIdentityRepeats_ThrowsExactException()
    {
        var first = Source("host", 1);
        var second = Source("host", 2);
        var exception = Should.Throw<ArgumentException>(() => Snapshot([Entry("agent.value", first)], [first, second]));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Equality_WhenOrderedSourceEvidenceDiffers_IsStructural()
    {
        var firstSource = Source("first", 1);
        var secondSource = Source("second", 1);
        var entry = new EffectiveConfigurationEntry(new ConfigurationPath("agent.value"), ConfigurationMergeOperation.Replace, JsonValue(), [firstSource, secondSource]);
        var first = Snapshot([entry], [firstSource, secondSource]);
        var same = Snapshot([entry], [firstSource, secondSource]);
        var reordered = Snapshot([entry], [secondSource, firstSource]);
        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(reordered);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("snapshot"), [], []);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static EffectiveConfigurationSnapshot Snapshot(ImmutableArray<EffectiveConfigurationEntry> entries, ImmutableArray<ConfigurationSourceReference> sources) => new(new ConfigurationVersion(1), new ContentHash("snapshot"), entries, sources);
    private static EffectiveConfigurationEntry Entry(string path, ConfigurationSourceReference source) => new(new ConfigurationPath(path), ConfigurationMergeOperation.Replace, JsonValue(), [source]);
    private static ConfigurationSourceReference Source(string id, long version) => new(new ConfigurationSourceId(id), new ConfigurationSourceVersion(version), ConfigurationLayerKind.HostGlobal, ConfigurationTrustClass.HostEstablished, new ContentHash($"hash-{id}-{version}"));
    private static ConfigurationJsonValue JsonValue()
    {
        using var document = JsonDocument.Parse("true");
        return new ConfigurationJsonValue(document.RootElement);
    }
}
