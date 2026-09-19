// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>An <see cref="IOutputBackpressurePolicy"/> test double that always returns one scripted decision.</summary>
internal sealed class FakeOutputBackpressurePolicy: IOutputBackpressurePolicy
{
    private readonly BackpressureDecision _decision;

    /// <summary>Initializes a policy that always returns <paramref name="decision"/>.</summary>
    /// <param name="decision">The decision returned for every call.</param>
    public FakeOutputBackpressurePolicy(BackpressureDecision decision) => _decision = decision;

    /// <summary>Gets every delivery/blockedFor pair this fake was asked to decide, in call order.</summary>
    public List<(RunEventDelivery Delivery, TimeSpan BlockedFor)> Requests { get; } = [];

    /// <inheritdoc/>
    public ValueTask<BackpressureDecision> DecideAsync(
        RunEventDelivery delivery, TimeSpan blockedFor, CancellationToken cancellationToken = default)
    {
        Requests.Add((delivery, blockedFor));
        return ValueTask.FromResult(_decision);
    }
}
