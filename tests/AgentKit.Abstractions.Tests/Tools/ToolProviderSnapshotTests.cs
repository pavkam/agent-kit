// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

public sealed class ToolProviderSnapshotTests
{
    [Fact]
    public void Constructor_WhenPublicationIsValid_RetainsSourceVersionAndDescriptorOrder()
    {
        var first = Descriptor("read", "1");
        var second = Descriptor("read", "2");
        var snapshot = new ToolProviderSnapshot(Source(), Version(), [second, first]);
        snapshot.SourceId.ShouldBe(Source());
        snapshot.SourceVersion.ShouldBe(Version());
        snapshot.Tools.ShouldBe([second, first]);
        snapshot.Tools[0].ShouldBeSameAs(second);
    }

    [Fact]
    public void Constructor_WhenPublicationIsEmpty_RetainsInitializedEmptySnapshot()
    {
        var snapshot = new ToolProviderSnapshot(Source(), Version(), []);
        snapshot.Tools.IsDefault.ShouldBeFalse();
        snapshot.Tools.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenRequiredValuesAreDefault_RejectsExactParameters()
    {
        AssertExact<ArgumentOutOfRangeException>(() => _ = new ToolProviderSnapshot(default, Version(), []), "sourceId");
        AssertExact<ArgumentOutOfRangeException>(() => _ = new ToolProviderSnapshot(Source(), default, []), "sourceVersion");
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), default), "tools");
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), [null!]), "tools");
    }

    [Theory]
    [InlineData("other")]
    [InlineData("SOURCE")]
    public void Constructor_WhenDescriptorBelongsToAnotherSource_RejectsExactParameter(string source)
    {
        var descriptor = Descriptor("read", "1", source: source);
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), [descriptor]), "tools");
    }

    [Fact]
    public void Constructor_WhenExactIdentityRepeats_RejectsEvenEquivalentDescriptors()
    {
        var first = Descriptor("read", "1");
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), [first, first]), "tools");
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), [first, Descriptor("read", "1")]), "tools");
        AssertExact<ArgumentException>(() => _ = new ToolProviderSnapshot(Source(), Version(), [first, Descriptor("read", "1", description: "changed")]), "tools");
    }

    [Fact]
    public void Constructor_WhenNamesRepeatOrIdentityCaseDiffers_PreservesDistinctIdentitiesForCatalogPolicy()
    {
        var first = Descriptor("read", "1");
        var second = Descriptor("Read", "1");
        var snapshot = new ToolProviderSnapshot(Source(), Version(), [first, second]);
        snapshot.Tools.ShouldBe([first, second]);
        snapshot.Tools[0].Name.ShouldBe(snapshot.Tools[1].Name);
    }

    [Fact]
    public void Equals_WhenPublicationIsReconstructed_UsesOrderedStructuralContent()
    {
        var first = new ToolProviderSnapshot(Source(), Version(), [Descriptor("read", "1"), Descriptor("write", "2")]);
        var second = new ToolProviderSnapshot(Source(), Version(), [Descriptor("read", "1"), Descriptor("write", "2")]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(second);
        new HashSet<ToolProviderSnapshot> { first, second }.Count.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenAnyPublicationEvidenceDiffers_DistinguishesSnapshots()
    {
        var first = Descriptor("read", "1");
        var second = Descriptor("write", "2");
        var original = new ToolProviderSnapshot(Source(), Version(), [first, second]);
        ToolProviderSnapshot[] changed =
        [
            new(Source(), new ToolSourceVersion("V1"), [first, second]),
            new(new ToolSourceId("other"), Version(), [Descriptor("read", "1", source: "other"), Descriptor("write", "2", source: "other")]),
            new(Source(), Version(), [second, first]),
            new(Source(), Version(), [first]),
            new(Source(), Version(), [first, Descriptor("write", "2", description: "changed")]),
        ];
        foreach (var snapshot in changed)
        {
            original.ShouldNotBe(snapshot);
        }
        original.Equals(null).ShouldBeFalse();
    }

    private static ToolSourceId Source() => new("source");
    private static ToolSourceVersion Version() => new("v1");
    private static ToolDescriptor Descriptor(string id, string version, string source = "source", string description = "description")
    {
        using var schema = JsonDocument.Parse("{}");
        return new ToolDescriptor(new ToolId(id), new ToolVersion(version), "same display name", description,
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), schema.RootElement),
            null, new ToolEffects(ToolEffect.ReadOnly, null, null), new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId(source), ExtensionData.Empty);
    }

    private static void AssertExact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
