// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted protected resource keeps its class and its exact canonical identifier text.</summary>
/// <remarks>
/// A resource is the narrowest thing a grant binds to, and an effecting boundary re-derives and compares its identifier
/// before acting. Any normalization introduced by storage would therefore authorize a different concrete effect than the one
/// the authority approved, so the identifier is asserted byte-for-byte rather than case- or separator-insensitively.
/// </remarks>
public sealed class JsonProtectedResourceTests
{
    /// <summary>Verifies a null resource is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonProtectedResource.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies every resource kind reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualResource()
    {
        foreach (var original in TestEvidenceFactory.Resources())
        {
            JsonProtectedResource.FromDomain(original).ToDomain().ShouldBe(original);
        }
    }

    /// <summary>Verifies the resource survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualResource()
    {
        foreach (var original in TestEvidenceFactory.Resources())
        {
            var document = TestCanonicalJson.Cycle(JsonProtectedResource.FromDomain(original));

            document.ToDomain().ShouldBe(original);
        }
    }

    /// <summary>Verifies the identifier is preserved verbatim, including casing and trailing separators.</summary>
    [Fact]
    public void ToDomain_WhenIdentifierHasSignificantText_PreservesItVerbatim()
    {
        var original = new ProtectedResource(ProtectedResourceKind.Directory, "/Workspace/Mixed Case/");

        var restored = TestCanonicalJson.Cycle(JsonProtectedResource.FromDomain(original)).ToDomain();

        restored.Identifier.ShouldBe("/Workspace/Mixed Case/");
        restored.ShouldBe(original);
    }

    /// <summary>Verifies the resource class is persisted as a stable member name rather than a reorderable ordinal.</summary>
    [Fact]
    public void FromDomain_WhenEncoded_WritesKindAsStableName()
    {
        var document = JsonProtectedResource.FromDomain(
            new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://example.invalid"));

        TestCanonicalJson.Encode(document).ShouldContain("\"NetworkEndpoint\"");
    }

    /// <summary>Verifies an undefined persisted kind fails closed rather than matching a later grant comparison.</summary>
    [Fact]
    public void ToDomain_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonProtectedResource((ProtectedResourceKind) 99, "/workspace/first.txt");

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("kind");
    }

    /// <summary>Verifies a blank persisted identifier is rejected rather than treated as an unbounded resource.</summary>
    [Fact]
    public void ToDomain_WhenIdentifierIsBlank_ThrowsArgumentException()
    {
        var document = new JsonProtectedResource(ProtectedResourceKind.File, "   ");

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("identifier");
    }

    /// <summary>Verifies a null persisted identifier is rejected before the domain resource is constructed.</summary>
    [Fact]
    public void ToDomain_WhenIdentifierIsNull_ThrowsArgumentNullException()
    {
        var document = new JsonProtectedResource(ProtectedResourceKind.File, null!);

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("identifier");
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal, which ordered resource sets rely on.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonProtectedResource.FromDomain(
            new ProtectedResource(ProtectedResourceKind.File, "/workspace/first.txt"));

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
