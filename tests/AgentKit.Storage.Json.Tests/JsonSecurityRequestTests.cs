// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted security request reproduces the operation as it stood before any decision or effect.</summary>
/// <remarks>
/// Persisting the request is what lets an audit trail explain why a grant exists and lets a deferred approval resolve against
/// exactly what was asked for. Two absences are meaningful and must survive unchanged: a request that no tool caused, and a
/// request issued through the unpinned path that carries no captured authorization.
/// </remarks>
public sealed class JsonSecurityRequestTests
{
    /// <summary>Verifies a null request is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonSecurityRequest.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a fully populated request reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualRequest()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: true, withToolCall: true);

        JsonSecurityRequest.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the request survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualRequest()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: true, withToolCall: true);

        var document = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies an unpinned request stays unpinned instead of gaining snapshot-bound authorization evidence.</summary>
    [Fact]
    public void ToDomain_WhenRequestHasNoCapturedAuthorization_DoesNotGainOne()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: false, withToolCall: true);
        original.Authorization.ShouldBeNull();

        var document = JsonSecurityRequest.FromDomain(original);
        document.Authorization.ShouldBeNull();

        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.Authorization.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a request issued with captured authorization keeps it through storage rather than losing it.</summary>
    [Fact]
    public void ToDomain_WhenRequestHasCapturedAuthorization_RetainsIt()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: true, withToolCall: false);

        var restored = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original)).ToDomain();

        _ = restored.Authorization.ShouldNotBeNull();
        restored.Authorization.ShouldBe(original.Authorization);
        restored.Authorization.Identity.ShouldBe(restored.Identity);
    }

    /// <summary>Verifies a request no tool caused never acquires a fabricated tool correlation.</summary>
    [Fact]
    public void ToDomain_WhenNoToolCallCausedTheRequest_LeavesToolCallIdNull()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: true, withToolCall: false);

        var document = JsonSecurityRequest.FromDomain(original);
        document.ToolCallId.ShouldBeNull();

        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.ToolCallId.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a tool-caused request keeps the exact causing tool-call identity.</summary>
    [Fact]
    public void ToDomain_WhenToolCallCausedTheRequest_RetainsToolCallId()
    {
        var original = TestEvidenceFactory.Request(withAuthorization: false, withToolCall: true);

        var restored = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original)).ToDomain();

        restored.ToolCallId.ShouldBe(original.ToolCallId);
    }

    /// <summary>Verifies a multi-use request is not replayed as a single-use request by relying on a constructor default.</summary>
    [Fact]
    public void ToDomain_WhenRequestedUsesExceedsOne_PreservesRequestedUses()
    {
        var original = Sample();
        original.RequestedUses.ShouldBe(4);

        var restored = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original)).ToDomain();

        restored.RequestedUses.ShouldBe(4);
    }

    /// <summary>Verifies the exclusive decision deadline survives unchanged, since a later approval resolves against it.</summary>
    [Fact]
    public void ToDomain_WhenDeadlineIsPersisted_ReproducesExactInstant()
    {
        var original = Sample();

        var restored = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original)).ToDomain();

        restored.Deadline.ShouldBe(original.Deadline);
    }

    /// <summary>Verifies named resources survive in the same order, since the request binds to that exact sequence.</summary>
    [Fact]
    public void ToDomain_WhenRequestNamesResources_PreservesResourceOrder()
    {
        var original = Sample();

        var restored = TestCanonicalJson.Cycle(JsonSecurityRequest.FromDomain(original)).ToDomain();

        restored.Resources.Select(static resource => resource.Identifier)
            .ShouldBe(original.Resources.Select(static resource => resource.Identifier));
    }

    /// <summary>Verifies a missing scope is refused rather than dereferenced while rebuilding the request.</summary>
    [Fact]
    public void ToDomain_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Scope = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Scope");
    }

    /// <summary>Verifies a missing identity is refused rather than dereferenced while rebuilding the request.</summary>
    [Fact]
    public void ToDomain_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Identity = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Identity");
    }

    /// <summary>Verifies a null resource element is rejected rather than dereferenced while rebuilding the request.</summary>
    [Fact]
    public void ToDomain_WhenResourcesContainNull_ThrowsArgumentException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Resources = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("Resources");
    }

    /// <summary>Verifies an empty resource set is rejected, because a request must name what it would touch.</summary>
    [Fact]
    public void ToDomain_WhenResourcesAreEmpty_ThrowsArgumentException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Resources = [] };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted request identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Id = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted tool-call identity is rejected rather than treated as an absent correlation.</summary>
    [Fact]
    public void ToDomain_WhenToolCallIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { ToolCallId = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted audience is rejected, since the enforcing component must be named exactly.</summary>
    [Fact]
    public void ToDomain_WhenAudienceIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Audience = " " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an undefined persisted effect fails closed instead of requesting an unknown material change.</summary>
    [Fact]
    public void ToDomain_WhenEffectIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { Effect = (SecurityEffect) 99 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted use count is rejected at the boundary just below one requested use.</summary>
    [Fact]
    public void ToDomain_WhenRequestedUsesIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityRequest.FromDomain(Sample());
        var document = source with { RequestedUses = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies documents compare by ordered resource contents, not by immutable-array backing storage identity.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityRequest.FromDomain(
            TestEvidenceFactory.Request(withAuthorization: true, withToolCall: true));

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies reordering resources breaks equality, proving order is compared rather than ignored.</summary>
    [Fact]
    public void Equals_WhenResourceOrderDiffers_ReturnsFalse()
    {
        var document = JsonSecurityRequest.FromDomain(Sample());
        var reordered = document with { Resources = [.. document.Resources.Reverse()] };

        document.Equals(reordered).ShouldBeFalse();
    }

    /// <summary>Verifies an absent tool correlation alone changes equality, since it is causal evidence.</summary>
    [Fact]
    public void Equals_WhenOneDocumentHasToolCorrelation_ReturnsFalse()
    {
        var correlated = JsonSecurityRequest.FromDomain(
            TestEvidenceFactory.Request(withAuthorization: false, withToolCall: true));
        var uncorrelated = JsonSecurityRequest.FromDomain(Sample());

        correlated.Equals(uncorrelated).ShouldBeFalse();
    }

    private static SecurityRequest Sample() =>
        TestEvidenceFactory.Request(withAuthorization: false, withToolCall: false);
}
