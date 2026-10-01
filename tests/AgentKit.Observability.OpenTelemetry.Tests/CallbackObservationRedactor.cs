// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>A redactor test double that delegates to a callback and counts invocations.</summary>
internal sealed class CallbackObservationRedactor(Func<ObservationContent, ObservationPolicy, RedactionResult> callback): IObservationRedactor
{
    private int _calls;

    /// <summary>Gets how many times redaction was requested.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc/>
    public ValueTask<RedactionResult> RedactAsync(
        ObservationContent content,
        ObservationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _calls);
        return ValueTask.FromResult(callback(content, policy));
    }
}
