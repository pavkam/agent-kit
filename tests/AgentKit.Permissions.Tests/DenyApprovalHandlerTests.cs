// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies the fail-closed default approval handler used when no channel is configured.</summary>
public sealed class DenyApprovalHandlerTests
{
    [Fact]
    public async Task TryResolveAsync_WhenCalled_ReturnsUnavailable()
    {
        var handler = new DenyApprovalHandler();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();

        var result = await handler.TryResolveAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalHandlerUnavailable>().SafeReason
            .ShouldBe("No approval handler is configured.");
    }

    [Fact]
    public async Task TryResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var handler = new DenyApprovalHandler();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => handler.TryResolveAsync(null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task TryResolveAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var handler = new DenyApprovalHandler();
        var request = SecurityAuthorityTestData.CreateApprovalRequest();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => handler.TryResolveAsync(request, cts.Token).AsTask());
    }
}
