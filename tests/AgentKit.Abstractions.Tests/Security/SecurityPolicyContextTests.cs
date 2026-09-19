// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityPolicyContext behavior and contracts.</summary>
public sealed class SecurityPolicyContextTests
{
    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityPolicyContext(null!, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var authorization = SecurityAbstractionsTestData.Authorization();
        var context = new SecurityPolicyContext(authorization, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch);
        context.Authorization.ShouldBe(authorization);
        context.RevocationVersion.ShouldBe(new SecurityRevocationVersion(1));
        context.EvaluatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityPolicyContext(SecurityAbstractionsTestData.Authorization(), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
