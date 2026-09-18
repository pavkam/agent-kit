// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that safe authentication evidence round-trips exactly, including an absent expiry.</summary>
/// <remarks>
/// This document records that a trusted ingress authenticated a subject; it must reproduce the issuer, method, instants, and
/// one-way fingerprint without acquiring an expiry the original did not have. An invented expiry would turn non-expiring
/// evidence into evidence that can silently lapse.
/// </remarks>
public sealed class JsonAuthenticationEvidenceTests
{
    /// <summary>Verifies null evidence is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonAuthenticationEvidence.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies expiring evidence reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualEvidence()
    {
        var original = TestEvidenceFactory.Evidence(withExpiry: true);

        JsonAuthenticationEvidence.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the evidence survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualEvidence()
    {
        var original = TestEvidenceFactory.Evidence(withExpiry: true);

        var document = TestCanonicalJson.Cycle(JsonAuthenticationEvidence.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies non-expiring evidence stays non-expiring rather than gaining a default instant.</summary>
    [Fact]
    public void FromDomain_WhenEvidenceDoesNotExpire_LeavesExpiresAtNull()
    {
        var original = TestEvidenceFactory.Evidence(withExpiry: false);

        var document = JsonAuthenticationEvidence.FromDomain(original);

        document.ExpiresAt.ShouldBeNull();
        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.ExpiresAt.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies every unwrapped member is carried verbatim, including the fingerprint's underlying hash text.</summary>
    [Fact]
    public void FromDomain_WhenProjected_CarriesEveryUnwrappedMember()
    {
        var original = TestEvidenceFactory.Evidence(withExpiry: true);

        var document = JsonAuthenticationEvidence.FromDomain(original);

        document.Id.ShouldBe(original.Id.Value);
        document.Issuer.ShouldBe(original.Issuer.Value);
        document.Method.ShouldBe(original.Method);
        document.AuthenticatedAt.ShouldBe(original.AuthenticatedAt);
        document.ExpiresAt.ShouldBe(original.ExpiresAt);
        document.SafeFingerprint.ShouldBe(original.SafeFingerprint.Hash.Value);
    }

    /// <summary>Verifies a blank persisted evidence reference is rejected instead of producing default-valued identity.</summary>
    [Fact]
    public void ToDomain_WhenIdIsBlank_ThrowsArgumentException()
    {
        var document = new JsonAuthenticationEvidence(
            "  ", "issuer-primary", "mutual-tls", TestEvidenceFactory.Instant, null, "sha256:fingerprint");

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted issuer is rejected, because provenance is what makes evidence usable.</summary>
    [Fact]
    public void ToDomain_WhenIssuerIsBlank_ThrowsArgumentException()
    {
        var document = new JsonAuthenticationEvidence(
            "evidence-reference", "\t", "mutual-tls", TestEvidenceFactory.Instant, null, "sha256:fingerprint");

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted method name is rejected rather than admitted as unnamed authentication.</summary>
    [Fact]
    public void ToDomain_WhenMethodIsBlank_ThrowsArgumentException()
    {
        var document = new JsonAuthenticationEvidence(
            "evidence-reference", "issuer-primary", "  ", TestEvidenceFactory.Instant, null, "sha256:fingerprint");

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("method");
    }

    /// <summary>Verifies a blank persisted fingerprint is rejected, since it could not correlate or partition anything.</summary>
    [Fact]
    public void ToDomain_WhenSafeFingerprintIsBlank_ThrowsArgumentException()
    {
        var document = new JsonAuthenticationEvidence(
            "evidence-reference", "issuer-primary", "mutual-tls", TestEvidenceFactory.Instant, null, " ");

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies corrupted clock evidence is refused rather than resurrected as already-expired identity evidence.</summary>
    [Fact]
    public void ToDomain_WhenExpiryIsNotLaterThanAuthentication_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonAuthenticationEvidence(
            "evidence-reference",
            "issuer-primary",
            "mutual-tls",
            TestEvidenceFactory.Instant,
            TestEvidenceFactory.Instant,
            "sha256:fingerprint");

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("expiresAt");
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by structure, not by reference.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonAuthenticationEvidence.FromDomain(TestEvidenceFactory.Evidence(withExpiry: true));

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
