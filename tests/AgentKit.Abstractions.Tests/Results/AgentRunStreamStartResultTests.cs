// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class AgentRunStreamStartResultTests
{
    [Fact]
    public void CopyConstructor_WhenExternalVariantCopiesBuiltIn_RejectsForeignVariant()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignVariant(new AgentRunStreamRejected<string>(new(RunResultTestData.Agent, RunResultTestData.Session, RunResultTestData.Error(AgentErrorCodes.InvalidInput)))));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new ForeignVariant(null!)).ParamName.ShouldBe("original");

    [Fact]
    public void CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndEvidence()
    {
        AgentRunStreamStartResult<string> original = new AgentRunStreamRejected<string>(new(RunResultTestData.Agent, RunResultTestData.Session, RunResultTestData.Error(AgentErrorCodes.InvalidInput)));
        var copy = original with { };
        copy.ShouldBe(original); copy.ShouldNotBeSameAs(original); copy.GetType().ShouldBe(original.GetType());
    }

    private sealed record ForeignVariant(AgentRunStreamStartResult<string> Original): AgentRunStreamStartResult<string>(Original);
}
