// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolEffectsTests
{
    [Fact]
    public void Constructor_WhenEnumsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolEffects((ToolEffect) 99, null, null)).ParamName.ShouldBe("effect");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolEffects(ToolEffect.ReadOnly, (IdempotencyClassification) 99, null)).ParamName.ShouldBe("idempotency");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolEffects(ToolEffect.ReadOnly, null, [(ProtectedResourceKind) 99])).ParamName.ShouldBe("requiredResourceKinds");
    }

    [Fact]
    public void Constructor_WhenResourcesDefaultOrDuplicate_ThrowsArgumentException()
    {
        ImmutableArray<ProtectedResourceKind> resources = default;
        Should.Throw<ArgumentException>(() => new ToolEffects(ToolEffect.ReadOnly, null, resources)).ParamName.ShouldBe("requiredResourceKinds");
        Should.Throw<ArgumentException>(() => new ToolEffects(ToolEffect.ReadOnly, null, [ProtectedResourceKind.File, ProtectedResourceKind.File])).ParamName.ShouldBe("requiredResourceKinds");
    }

    [Fact]
    public void Constructor_WhenMutatingEffectClaimsReadOnlyIdempotency_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.ReadOnly, null));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("idempotency");
    }

    [Fact]
    public void Constructor_WhenResourcesUnasserted_DistinguishesExplicitNone()
    {
        var unasserted = new ToolEffects(ToolEffect.ReadOnly, null, null);
        var explicitNone = new ToolEffects(ToolEffect.ReadOnly, null, []);

        unasserted.RequiredResourceKinds.ShouldBeNull();
        explicitNone.RequiredResourceKinds.ShouldBe(ImmutableArray<ProtectedResourceKind>.Empty);
        unasserted.ShouldNotBe(explicitNone);
    }

    [Fact]
    public void Equality_WhenResourcesEqual_PreservesOrderAndHash()
    {
        var first = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey,
            [ProtectedResourceKind.File, ProtectedResourceKind.Directory]);
        var same = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey,
            [ProtectedResourceKind.File, ProtectedResourceKind.Directory]);
        var reordered = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey,
            [ProtectedResourceKind.Directory, ProtectedResourceKind.File]);

        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(reordered);
    }
}
