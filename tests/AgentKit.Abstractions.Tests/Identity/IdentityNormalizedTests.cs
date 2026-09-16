// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityNormalized behavior and contracts.</summary>
public sealed class IdentityNormalizedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var identity = Identity();
        var normalized = new IdentityNormalized(identity);
        normalized.Identity.ShouldBe(identity);
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new IdentityNormalized(null!));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        IdentityNormalizationResult first = new IdentityNormalized(Identity());
        IdentityNormalizationResult second = new IdentityNormalized(Identity());
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityNormalized(Identity());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
