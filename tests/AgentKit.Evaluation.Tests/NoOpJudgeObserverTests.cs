// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class NoOpJudgeObserverTests
{
    [Fact]
    public async Task OnEventAsync_WhenCalled_CompletesWithoutRetainingTheEvent()
    {
        var modelRequest = new ModelRequestId(Guid.NewGuid());
        var responseEvent = new ModelResponseStarted(modelRequest, 1);

        await NoOpJudgeObserver.Instance.OnEventAsync(responseEvent, TestContext.Current.CancellationToken);

        NoOpJudgeObserver.Instance.ShouldBeSameAs(NoOpJudgeObserver.Instance);
    }
}
