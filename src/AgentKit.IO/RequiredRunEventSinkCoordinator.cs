// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Completes shutdown after required run-event sinks observe the configured delivery deadline.</summary>
internal sealed class RequiredRunEventSinkCoordinator(
    IEnumerable<IRunEventSink> sinks,
    TimeProvider timeProvider): IRequiredRunEventSinkCoordinator
{
    private readonly ImmutableArray<IRunEventSink> _sinks = [.. sinks];
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async ValueTask DrainAsync(TimeSpan deadline, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero, nameof(deadline));
        var required = _sinks
            .OfType<RunEventSinkBinding>()
            .Where(static binding => binding.Registration.Delivery == RunEventDelivery.Required)
            .Select(static binding => binding)
            .ToArray();
        if (required.Length == 0)
        {
            return;
        }

        var started = _timeProvider.GetTimestamp();
        while (_timeProvider.GetElapsedTime(started) < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(50), _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
