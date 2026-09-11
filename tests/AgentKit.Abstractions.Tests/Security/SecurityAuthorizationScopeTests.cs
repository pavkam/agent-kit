// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuthorizationScope behavior and contracts.</summary>
public sealed class SecurityAuthorizationScopeTests
{
    /// <summary>Verifies canonical scope and correlation values reject default nested identities, including record-copy mutation.</summary>
    [Fact]
    public void Constructor_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationScope(default, null, Correlation())).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationScope(AgentId(), default(SessionId), Correlation())).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationScope(AgentId(), SessionId(), null!)).ParamName.ShouldBe("correlation");
        Should.Throw<ArgumentNullException>(() => Scope() with { Correlation = null! }).ParamName.ShouldBe("correlation");
    }

    /// <summary>Verifies every hardened init accessor rejects invalid record copies without changing the original.</summary>
    [Fact]
    public void With_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgumentAndPreservesOriginal()
    {
        var scope = Scope();
        Should.Throw<ArgumentOutOfRangeException>(() => scope with { AgentId = default }).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => scope with { SessionId = default(SessionId) }).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentNullException>(() => scope with { Correlation = null! }).ParamName.ShouldBe("correlation");
        scope.ShouldBe(Scope());
    }

    private static AgentId AgentId() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static SessionId SessionId() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static BeforeRunOperationCorrelation Correlation() => new(OperationId(), null);
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
}
