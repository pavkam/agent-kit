// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;



/// <summary>Verifies ContextPreparationFailure behavior and contracts.</summary>
public sealed class ContextPreparationFailureTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
        failure.Kind.ShouldBe(ContextPreparationFailureKind.Unknown);
        failure.SafeMessage.ShouldBe("unavailable");
        failure.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, " ", ExtensionData.Empty));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, "unavailable", null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
        var second = new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
        var copy = original with { SafeMessage = "changed" };
        copy.SafeMessage.ShouldBe("changed");
        original.SafeMessage.ShouldBe("unavailable");
    }
}
