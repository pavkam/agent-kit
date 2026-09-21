// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextOverflowBehavior"/> values.</summary>
public sealed class ContextOverflowBehaviorTests
{
    [Fact]
    public void ContextOverflowBehavior_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<ContextOverflowBehavior>().ShouldBe([ContextOverflowBehavior.Fail, ContextOverflowBehavior.CompactWhenConfigured]);
}
