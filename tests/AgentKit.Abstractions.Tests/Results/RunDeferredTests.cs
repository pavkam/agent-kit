// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class RunDeferredTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("runtime")]
    [InlineData("session")]
    [InlineData("run")]
    public void Constructor_WhenHandoffIsInvalid_RejectsExactArgument(string invalid)
    {
        var basis = RunResultTestData.Deferred(); var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        ImmutableArray<DeferredOperationRequest> requests = invalid switch
        {
            "default" => default,
            "empty" => [],
            "null" => [null!],
            "duplicate" => [basis, basis],
            "runtime" => [RunResultTestData.Deferred(kind: DeferralKind.ProviderSuspended, owner: DeferralContinuationOwner.RuntimeOperation, effects: DeferralEffectState.Started)],
            "session" => [basis, RunResultTestData.Deferred(2, session: new SessionId(id))],
            _ => [basis, RunResultTestData.Deferred(2, run: new RunId(id))],
        };
        var exception = Should.Throw<ArgumentException>(() => new RunDeferred(requests));
        exception.ParamName.ShouldBe("requests");
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Equals_WhenRequestsAreReconstructed_PreservesOrderedValueEquality()
    {
        var first = new RunDeferred([RunResultTestData.Deferred(), RunResultTestData.Deferred(2)]);
        var second = new RunDeferred([RunResultTestData.Deferred(), RunResultTestData.Deferred(2)]);
        first.ShouldBe(second); first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(new RunDeferred([RunResultTestData.Deferred(2), RunResultTestData.Deferred()]));
    }
}
