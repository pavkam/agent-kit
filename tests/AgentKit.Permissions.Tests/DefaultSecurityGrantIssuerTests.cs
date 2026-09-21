// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Options;

/// <summary>Verifies <see cref="DefaultSecurityGrantIssuer"/> behavior and contracts.</summary>
public sealed class DefaultSecurityGrantIssuerTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsync_WhenApprovedBindingExpiresBeforeHostCeiling_BoundsGrantExpiry()
    {
        var clock = new FakeTimeProvider(_now);
        var issuer = new DefaultSecurityGrantIssuer(
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions { MaximumGrantLifetime = TimeSpan.FromMinutes(30) }));
        var request = SecurityAuthorityTestData.CreateRequest(_now) with { Deadline = _now.AddHours(2) };
        var binding = new ApprovalScopeBinding(
            request,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            _now,
            _now.AddMinutes(5),
            1);

        var grant = await issuer.IssueAsync(
            request,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            binding,
            TestContext.Current.CancellationToken);

        grant.ExpiresAt.ShouldBe(_now.AddMinutes(5));
    }

    private sealed class StubGrantIdGenerator: IIdentifierGenerator<GrantId>
    {
        public GrantId Create() => new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
    }
}
