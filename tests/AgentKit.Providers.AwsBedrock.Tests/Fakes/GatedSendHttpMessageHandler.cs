// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Fakes;

using System.Diagnostics;
using System.Net.Http;

/// <summary>
/// An <see cref="HttpMessageHandler"/> test double whose
/// <see cref="SendAsync(HttpRequestMessage, CancellationToken)"/> blocks
/// behind a deterministic entry signal until the caller-supplied
/// <see cref="CancellationToken"/> is cancelled, so a test can interrupt an
/// adapter precisely while a request is in flight (before any response is
/// received) without any wall-clock waiting.
/// </summary>
/// <remarks>
/// <see cref="Entered"/> completes once the handler has recorded the
/// request and is waiting on the token. The handler never returns a
/// response: cancelling the linked token is the only way the pending send
/// completes, exactly as a real transport's send would surface
/// cancellation or a deadline. The handler is not thread-safe beyond the
/// signal handoff; tests drive it from a single in-flight request.
/// </remarks>
internal sealed class GatedSendHttpMessageHandler: HttpMessageHandler
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a signal that completes once a send reaches the gate.</summary>
    /// <value>A task that completes once the pending send is blocked and waiting.</value>
    public Task Entered => _entered.Task;

    /// <summary>Gets every request this handler has received, in receipt order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        _ = _entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        throw new UnreachableException("Task.Delay with an infinite timeout only completes by throwing for its token.");
    }
}
