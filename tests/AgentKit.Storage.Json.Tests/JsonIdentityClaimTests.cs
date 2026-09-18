// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted claim keeps its issuer provenance, normalized text, and value interpretation.</summary>
/// <remarks>
/// A claim is only usable in a policy decision because its issuer is recorded alongside it, and its value stays text for
/// every kind so the issuer's normalization is not re-encoded by a JSON writer. Both properties are asserted for each defined
/// value kind rather than only for text.
/// </remarks>
public sealed class JsonIdentityClaimTests
{
    /// <summary>Verifies a null claim is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonIdentityClaim.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a claim reconstructs as an equal domain value including its issuer and value kind.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualClaim()
    {
        var original = new IdentityClaim(
            new IdentityIssuerId("issuer-primary"), "role", "operator", IdentityClaimValueKind.Text);

        JsonIdentityClaim.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies every defined value kind survives a real canonical encode and decode.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEveryValueKind()
    {
        foreach (var original in TestEvidenceFactory.Claims())
        {
            var restored = TestCanonicalJson.Cycle(JsonIdentityClaim.FromDomain(original)).ToDomain();

            restored.ShouldBe(original);
            restored.ValueKind.ShouldBe(original.ValueKind);
        }
    }

    /// <summary>Verifies a non-text value stays the issuer's canonical text rather than being re-encoded as a JSON scalar.</summary>
    [Fact]
    public void FromDomain_WhenValueKindIsWholeNumber_KeepsCanonicalTextEncoding()
    {
        var original = new IdentityClaim(
            new IdentityIssuerId("issuer-primary"), "tier", "007", IdentityClaimValueKind.WholeNumber);

        var document = JsonIdentityClaim.FromDomain(original);

        document.Value.ShouldBe("007");
        TestCanonicalJson.Encode(document).ShouldContain("\"007\"");
        TestCanonicalJson.Cycle(document).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the value kind is persisted as a stable member name rather than a reorderable ordinal.</summary>
    [Fact]
    public void FromDomain_WhenEncoded_WritesValueKindAsStableName()
    {
        var document = JsonIdentityClaim.FromDomain(new IdentityClaim(
            new IdentityIssuerId("issuer-primary"), "mfa", "true", IdentityClaimValueKind.Boolean));

        TestCanonicalJson.Encode(document).ShouldContain("\"Boolean\"");
    }

    /// <summary>Verifies a blank persisted issuer is rejected rather than admitted into a policy decision.</summary>
    [Fact]
    public void ToDomain_WhenIssuerIsBlank_ThrowsArgumentException()
    {
        var document = new JsonIdentityClaim(" ", "role", "operator", IdentityClaimValueKind.Text);

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted claim type is rejected rather than admitted as an unnamed claim.</summary>
    [Fact]
    public void ToDomain_WhenTypeIsBlank_ThrowsArgumentException()
    {
        var document = new JsonIdentityClaim("issuer-primary", "\t", "operator", IdentityClaimValueKind.Text);

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("type");
    }

    /// <summary>Verifies a blank persisted claim value is rejected rather than admitted as an empty assertion.</summary>
    [Fact]
    public void ToDomain_WhenValueIsBlank_ThrowsArgumentException()
    {
        var document = new JsonIdentityClaim("issuer-primary", "role", "  ", IdentityClaimValueKind.Text);

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a value kind outside the defined range fails closed rather than defaulting to text.</summary>
    [Fact]
    public void ToDomain_WhenValueKindIsOutsideDefinedRange_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonIdentityClaim("issuer-primary", "role", "operator", (IdentityClaimValueKind) 99);

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("valueKind");
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by structure, not by reference.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonIdentityClaim.FromDomain(new IdentityClaim(
            new IdentityIssuerId("issuer-primary"), "role", "operator", IdentityClaimValueKind.Text));

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
