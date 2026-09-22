// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Discards live progress reports for invocations that do not require streaming updates.</summary>
internal sealed class NoopToolProgressReporter: IToolProgressReporter
{
    /// <summary>Gets the shared no-op reporter instance.</summary>
    internal static NoopToolProgressReporter Instance { get; } = new();

    /// <inheritdoc/>
    public ValueTask ReportAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
