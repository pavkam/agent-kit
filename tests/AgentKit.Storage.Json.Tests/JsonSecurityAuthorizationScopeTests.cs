// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted authorization scope reconstructs the exact agent, session, and causal correlation.</summary>
/// <remarks>
/// The scope is the binding a grant, an enforcement request, and an audit record must all agree on, so any drift across a
/// round trip would silently widen or narrow what a later enforcement check compares against. A fabricated session binding is
/// the specific widening this mirror must never introduce.
/// </remarks>
public sealed class JsonSecurityAuthorizationScopeTests
{
    /// <summary>Verifies a null scope is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonSecurityAuthorizationScope.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a session-bound scope reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualScope()
    {
        var original = TestEvidenceFactory.Scope();

        JsonSecurityAuthorizationScope.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the scope survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualScope()
    {
        var original = TestEvidenceFactory.Scope();

        var document = TestCanonicalJson.Cycle(JsonSecurityAuthorizationScope.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies a sessionless operation is preserved as sessionless rather than gaining an empty session binding.</summary>
    [Fact]
    public void FromDomain_WhenScopeHasNoSession_LeavesSessionIdNull()
    {
        var original = TestEvidenceFactory.Scope(withSession: false);

        var document = JsonSecurityAuthorizationScope.FromDomain(original);

        document.SessionId.ShouldBeNull();
        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.SessionId.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies the polymorphic correlation is delegated intact rather than flattened into the scope.</summary>
    [Fact]
    public void ToDomain_WhenCorrelationIsAfterRun_PreservesConcreteCorrelationKind()
    {
        var original = new SecurityAuthorizationScope(
            TestEvidenceFactory.Agent, TestEvidenceFactory.Session, TestEvidenceFactory.AfterRun());

        var restored = TestCanonicalJson.Cycle(JsonSecurityAuthorizationScope.FromDomain(original)).ToDomain();

        _ = restored.Correlation.ShouldBeOfType<AfterRunOperationCorrelation>();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a missing correlation is refused rather than dereferenced, which a well-formed document never omits.</summary>
    [Fact]
    public void ToDomain_WhenCorrelationIsNull_ThrowsArgumentNullException()
    {
        var document = new JsonSecurityAuthorizationScope(TestEvidenceFactory.Agent.Value, null, null!);

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Correlation");
    }

    /// <summary>Verifies an empty persisted agent identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenAgentIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonSecurityAuthorizationScope(
            Guid.Empty, null, JsonOperationCorrelation.FromDomain(TestEvidenceFactory.InRun(withTurn: true)));

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted session identity is rejected rather than treated as an absent session.</summary>
    [Fact]
    public void ToDomain_WhenSessionIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonSecurityAuthorizationScope(
            TestEvidenceFactory.Agent.Value,
            Guid.Empty,
            JsonOperationCorrelation.FromDomain(TestEvidenceFactory.InRun(withTurn: true)));

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by structure, not by reference.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityAuthorizationScope.FromDomain(TestEvidenceFactory.Scope());

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
