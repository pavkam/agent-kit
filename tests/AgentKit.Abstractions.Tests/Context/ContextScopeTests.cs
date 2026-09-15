// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextScope"/> values.</summary>
public sealed class ContextScopeTests
{
    [Fact]
    public void ContextScope_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<ContextScope>().ShouldBe([ContextScope.Engine, ContextScope.Agent, ContextScope.Session, ContextScope.Conversation, ContextScope.Run, ContextScope.Turn, ContextScope.ModelRequest]);
}
