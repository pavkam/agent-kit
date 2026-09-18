// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted policy-snapshot reference reproduces its identity, version, and content fingerprint.</summary>
/// <remarks>
/// The reference is captured evidence rather than authority, so the property that matters is exactness: a default-valued
/// reference would compare equal to another corrupted reference and let a consumer believe two unrelated decisions shared one
/// effective policy snapshot.
/// </remarks>
public sealed class JsonSecurityPolicySnapshotReferenceTests
{
    /// <summary>Verifies a null reference is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => JsonSecurityPolicySnapshotReference.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies the reference reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualReference()
    {
        var original = TestEvidenceFactory.PolicySnapshot();

        JsonSecurityPolicySnapshotReference.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the reference survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualReference()
    {
        var original = TestEvidenceFactory.PolicySnapshot();

        var document = TestCanonicalJson.Cycle(JsonSecurityPolicySnapshotReference.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies each wrapper value is unwrapped to a JSON-native primitive rather than a domain type.</summary>
    [Fact]
    public void FromDomain_WhenProjected_CarriesUnwrappedPrimitives()
    {
        var original = TestEvidenceFactory.PolicySnapshot();

        var document = JsonSecurityPolicySnapshotReference.FromDomain(original);

        document.Id.ShouldBe(original.Id.Value);
        document.Version.ShouldBe(original.Version.Value);
        document.Fingerprint.ShouldBe(original.Fingerprint.Value);
    }

    /// <summary>Verifies an empty persisted snapshot identity is rejected instead of rebuilt as a default reference.</summary>
    [Fact]
    public void ToDomain_WhenIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonSecurityPolicySnapshotReference(Guid.Empty, 7, "sha256:policy-snapshot");

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a zero persisted version is rejected at the boundary just below the first published version.</summary>
    [Fact]
    public void ToDomain_WhenVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonSecurityPolicySnapshotReference(
            Guid.Parse("99999999-9999-9999-9999-999999999999"), 0, "sha256:policy-snapshot");

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a negative persisted version is rejected rather than treated as an unpublished snapshot.</summary>
    [Fact]
    public void ToDomain_WhenVersionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonSecurityPolicySnapshotReference(
            Guid.Parse("99999999-9999-9999-9999-999999999999"), -1, "sha256:policy-snapshot");

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted fingerprint is rejected, since it could not identify an effective policy set.</summary>
    [Fact]
    public void ToDomain_WhenFingerprintIsBlank_ThrowsArgumentException()
    {
        var document = new JsonSecurityPolicySnapshotReference(
            Guid.Parse("99999999-9999-9999-9999-999999999999"), 7, "   ");

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by structure, not by reference.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityPolicySnapshotReference.FromDomain(TestEvidenceFactory.PolicySnapshot());

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
