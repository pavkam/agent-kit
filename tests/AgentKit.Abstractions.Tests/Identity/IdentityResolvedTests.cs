// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityResolved behavior and contracts.</summary>
public sealed class IdentityResolvedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var identity = Identity();
        var resolved = new IdentityResolved(identity);
        resolved.Identity.ShouldBe(identity);
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new IdentityResolved(null!));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        IdentityResolutionResult first = new IdentityResolved(Identity());
        IdentityResolutionResult second = new IdentityResolved(Identity());
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityResolved(Identity());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
