// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;



/// <summary>Verifies ContextPreparationFailed behavior and contracts.</summary>
public sealed class ContextPreparationFailedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = Failure();
        var failed = new ContextPreparationFailed(failure);
        failed.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextPreparationFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        ContextAssemblyResult first = new ContextPreparationFailed(Failure());
        ContextAssemblyResult second = new ContextPreparationFailed(Failure());
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new ContextPreparationFailed(Failure());
        var replacement = new ContextPreparationFailure(ContextPreparationFailureKind.EmptyHistory, "empty", ExtensionData.Empty);
        var copy = original with { Failure = replacement };
        copy.Failure.ShouldBe(replacement);
    }

    private static ContextPreparationFailure Failure() => new(ContextPreparationFailureKind.Unknown, "unavailable", ExtensionData.Empty);
}
