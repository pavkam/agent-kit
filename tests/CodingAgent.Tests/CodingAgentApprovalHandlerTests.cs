// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Verifies terminal UI decisions preserve the authority-owned approval request exactly.</summary>
public sealed class CodingAgentApprovalHandlerTests
{
    [Theory]
    [InlineData(true, ApprovalResolution.Approved)]
    [InlineData(false, ApprovalResolution.Denied)]
    public async Task TryResolveAsync_WhenPromptCompletes_ReturnsExactAuthenticatedResponse(
        bool approved,
        ApprovalResolution expectedResolution)
    {
        var identity = ApprovalTestData.Identity();
        var request = ApprovalTestData.Request(identity);
        var prompt = new DelegateApprovalPrompt((actual, _) =>
        {
            actual.ShouldBeSameAs(request);
            return Task.FromResult(approved);
        });
        var handler = new CodingAgentApprovalHandler(prompt, identity, new MutableTimeProvider(ApprovalTestData.Now));

        var result = await handler.TryResolveAsync(request, TestContext.Current.CancellationToken);

        var response = result.ShouldBeOfType<ApprovalHandlerResponded>().Response;
        response.RequestId.ShouldBe(request.Id);
        response.Binding.ShouldBe(request.Binding);
        response.ApproverIdentity.ShouldBe(identity);
        response.Resolution.ShouldBe(expectedResolution);
        response.Id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TryResolveAsync_WhenPromptIsCancelled_PropagatesCancellationWithoutResponse()
    {
        var identity = ApprovalTestData.Identity();
        var request = ApprovalTestData.Request(identity);
        var handler = new CodingAgentApprovalHandler(
            new DelegateApprovalPrompt((_, cancellationToken) => Task.FromCanceled<bool>(cancellationToken)),
            identity,
            new MutableTimeProvider(ApprovalTestData.Now));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await handler.TryResolveAsync(request, cancellation.Token));
    }

    [Fact]
    public async Task TryResolveAsync_WhenApprovalCrossesDeadline_ReturnsDeniedResponse()
    {
        var identity = ApprovalTestData.Identity();
        var request = ApprovalTestData.Request(identity);
        var clock = new MutableTimeProvider(ApprovalTestData.Now);
        var handler = new CodingAgentApprovalHandler(
            new DelegateApprovalPrompt((_, _) =>
            {
                clock.Advance(TimeSpan.FromMinutes(6));
                return Task.FromResult(true);
            }),
            identity,
            clock);

        var result = await handler.TryResolveAsync(request, TestContext.Current.CancellationToken);

        var response = result.ShouldBeOfType<ApprovalHandlerResponded>().Response;
        response.Resolution.ShouldBe(ApprovalResolution.Denied);
        response.RespondedAt.ShouldBe(clock.GetUtcNow());
    }
}
