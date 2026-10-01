// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="Provenance"/> constraints and value semantics.</summary>
public sealed class ProvenanceTests
{
    [Fact]
    public void Constructor_WhenSourceKindIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new Provenance(null!)).ParamName.ShouldBe("sourceKind");

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSourceKindIsBlank_ThrowsArgumentException(string sourceKind) =>
        Should.Throw<ArgumentException>(() => new Provenance(sourceKind)).ParamName.ShouldBe("sourceKind");

    [Fact]
    public void Constructor_WhenOptionalPartsAreOmitted_UsesEmptyExtensionsAndNoSource()
    {
        var provenance = new Provenance("user");

        provenance.SourceRunId.ShouldBeNull();
        provenance.SourceSessionId.ShouldBeNull();
        provenance.SourceMessageId.ShouldBeNull();
        provenance.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Constructor_WhenSourceIdentitiesAreSupplied_PreservesThemExactly()
    {
        var run = new RunId(Guid.NewGuid());
        var session = new SessionId(Guid.NewGuid());
        var message = new MessageId(Guid.NewGuid());

        var provenance = new Provenance("assistant", run, session, message);

        provenance.SourceKind.ShouldBe("assistant");
        provenance.SourceRunId.ShouldBe(run);
        provenance.SourceSessionId.ShouldBe(session);
        provenance.SourceMessageId.ShouldBe(message);
    }

    [Fact]
    public void Equality_WhenAllPartsMatch_IsStructural()
    {
        var run = new RunId(Guid.NewGuid());

        new Provenance("user", run).ShouldBe(new Provenance("user", run));
        new Provenance("user", run).ShouldNotBe(new Provenance("tool", run));
    }
}
