// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class SecurityAllowConstraintsAlgebraTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryIntersect_WhenResourceSetsDisjoint_ReturnsFalse()
    {
        var request = SecurityAuthorityTestData.CreateRequest(_now);
        var first = new SecurityAllowConstraints(
            ImmutableArray.Create(new ProtectedResource(ProtectedResourceKind.File, "/a.txt")),
            null,
            null,
            null,
            null);
        var second = new SecurityAllowConstraints(
            ImmutableArray.Create(new ProtectedResource(ProtectedResourceKind.File, "/b.txt")),
            null,
            null,
            null,
            null);

        SecurityAllowConstraintsAlgebra.TryIntersect(
                [first, second],
                request,
                _now.AddMinutes(10),
                hostMaximumUses: 5,
                out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryIntersect_WhenEffectsConflict_ReturnsFalse()
    {
        var request = SecurityAuthorityTestData.CreateRequest(_now);
        var observe = new SecurityAllowConstraints(null, SecurityEffect.Observe, null, null, null);
        var execute = new SecurityAllowConstraints(null, SecurityEffect.Execute, null, null, null);

        SecurityAllowConstraintsAlgebra.TryIntersect(
                [observe, execute],
                request,
                _now.AddMinutes(10),
                hostMaximumUses: 5,
                out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryIntersect_WhenContributionsOverlap_ReturnsTrue()
    {
        var request = SecurityAuthorityTestData.CreateRequest(_now);
        var shared = request.Resources[0];
        var first = new SecurityAllowConstraints(ImmutableArray.Create(shared), null, null, null, 3);
        var second = new SecurityAllowConstraints(ImmutableArray.Create(shared), null, _now, null, 2);

        SecurityAllowConstraintsAlgebra.TryIntersect(
                [first, second],
                request,
                _now.AddMinutes(10),
                hostMaximumUses: 5,
                out var intersection)
            .ShouldBeTrue();
        _ = intersection.ShouldNotBeNull();
        intersection!.AllowedUses.ShouldBe(2);
        intersection.NotBefore.ShouldBe(_now);
    }
}
