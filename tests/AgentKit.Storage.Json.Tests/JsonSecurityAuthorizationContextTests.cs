// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that captured authorization evidence reproduces every selection, revision, scope, and identity exactly.</summary>
/// <remarks>
/// This document records which selections were observed when an operation was authorized. It is evidence, not a grant, so the
/// mirror's job is fidelity: the typed authority-key selection must rebind to the same closed contract, and the nested
/// snapshot, scope, and identity must survive unchanged so a later revalidation compares against what actually happened.
/// </remarks>
public sealed class JsonSecurityAuthorizationContextTests
{
    /// <summary>Verifies a null context is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => JsonSecurityAuthorizationContext.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies captured authorization reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualContext()
    {
        var original = Sample();

        JsonSecurityAuthorizationContext.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the context survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualContext()
    {
        var original = Sample();

        var document = TestCanonicalJson.Cycle(JsonSecurityAuthorizationContext.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the typed authority-key selection is rebound to the same closed service contract on read.</summary>
    [Fact]
    public void ToDomain_WhenAuthorityKeyIsPersisted_RebindsToSecurityAuthorityContract()
    {
        var original = Sample();

        var restored = TestCanonicalJson.Cycle(JsonSecurityAuthorizationContext.FromDomain(original)).ToDomain();

        restored.AuthorityKey.ShouldBe(new ComponentKey<ISecurityAuthority>("authority-primary"));
    }

    /// <summary>Verifies a zero agent-definition revision is retained, because that revision is legitimately non-negative.</summary>
    [Fact]
    public void ToDomain_WhenAgentDefinitionRevisionIsZero_RetainsZero()
    {
        var original = Sample();

        var restored = TestCanonicalJson.Cycle(JsonSecurityAuthorizationContext.FromDomain(original)).ToDomain();

        restored.AgentDefinitionRevision.Value.ShouldBe(0);
    }

    /// <summary>Verifies a sessionless captured scope stays sessionless through the nested scope projection.</summary>
    [Fact]
    public void ToDomain_WhenCapturedScopeHasNoSession_LeavesSessionNull()
    {
        var scope = TestEvidenceFactory.Scope(withSession: false);
        var original = TestEvidenceFactory.Authorization(scope, TestEvidenceFactory.Identity());

        var restored = TestCanonicalJson.Cycle(JsonSecurityAuthorizationContext.FromDomain(original)).ToDomain();

        restored.Scope.SessionId.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a missing policy snapshot is refused rather than dereferenced.</summary>
    [Fact]
    public void ToDomain_WhenPolicySnapshotIsNull_ThrowsArgumentNullException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { PolicySnapshot = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("PolicySnapshot");
    }

    /// <summary>Verifies a missing captured scope is refused rather than dereferenced.</summary>
    [Fact]
    public void ToDomain_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { Scope = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Scope");
    }

    /// <summary>Verifies a missing captured identity is refused rather than dereferenced.</summary>
    [Fact]
    public void ToDomain_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { Identity = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Identity");
    }

    /// <summary>Verifies a blank persisted profile key is rejected instead of selecting an unnamed profile.</summary>
    [Fact]
    public void ToDomain_WhenProfileKeyIsBlank_ThrowsArgumentException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { ProfileKey = " " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted authority key is rejected instead of selecting an unnamed authority.</summary>
    [Fact]
    public void ToDomain_WhenAuthorityKeyIsBlank_ThrowsArgumentException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { AuthorityKey = "\t" };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted profile version is rejected at the boundary just below the first revision.</summary>
    [Fact]
    public void ToDomain_WhenProfileVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { ProfileVersion = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a negative persisted agent-definition revision is rejected rather than clamped to zero.</summary>
    [Fact]
    public void ToDomain_WhenAgentDefinitionRevisionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { AgentDefinitionRevision = -1 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted configuration version is rejected rather than treated as unconfigured.</summary>
    [Fact]
    public void ToDomain_WhenConfigurationVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample()) with { ConfigurationVersion = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by nested structure.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityAuthorizationContext.FromDomain(Sample());

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static SecurityAuthorizationContext Sample() =>
        TestEvidenceFactory.Authorization(TestEvidenceFactory.Scope(), TestEvidenceFactory.Identity());
}
