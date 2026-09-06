// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

public sealed class NeverRetireSessionRetentionPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_WhenCalled_AlwaysDecidesKeep()
    {
        var policy = new NeverRetireSessionRetentionPolicy();

        var decision = await policy.EvaluateAsync(TestFactory.Descriptor(), TestContext.Current.CancellationToken);

        decision.Action.ShouldBe(SessionRetentionAction.Keep);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        var policy = new NeverRetireSessionRetentionPolicy();

        _ = await Should.ThrowAsync<ArgumentNullException>(
            async () => await policy.EvaluateAsync(null!, TestContext.Current.CancellationToken));
    }
}
