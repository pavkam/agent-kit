// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Binds one built-in tool package's package-owned log events to <see cref="ToolLeafObservation"/>.</summary>
/// <remarks>
/// Each tool package owns its event IDs and message templates in its own source-generated log class, so no package
/// shares an event identifier with another. This immutable value carries the three terminal events of one invocation
/// as delegates over those generated methods, which keeps the shared invocation wrapper free of any package's event IDs.
/// The delegates must be content-free: they receive only the tool identity, the call identity, and a bounded label.
/// </remarks>
public sealed record ToolLeafLogEvents
{
    /// <summary>Initializes the bound terminal events of one tool package.</summary>
    /// <param name="completed">Logs a terminal invocation outcome: logger, tool identity, call identity, bounded outcome label.</param>
    /// <param name="cancelled">Logs caller cancellation: logger, tool identity, call identity.</param>
    /// <param name="faulted">Logs an unexpected invocation exception: logger, tool identity, call identity, exception type name.</param>
    /// <exception cref="ArgumentNullException">Any delegate is null.</exception>
    public ToolLeafLogEvents(
        Action<ILogger, ToolId, ToolCallId, string> completed,
        Action<ILogger, ToolId, ToolCallId> cancelled,
        Action<ILogger, ToolId, ToolCallId, string> faulted)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(cancelled);
        ArgumentNullException.ThrowIfNull(faulted);
        Completed = completed;
        Cancelled = cancelled;
        Faulted = faulted;
    }

    /// <summary>Gets the event logged when an invocation reaches a terminal result.</summary>
    public Action<ILogger, ToolId, ToolCallId, string> Completed { get; }

    /// <summary>Gets the event logged when the caller cancels an invocation.</summary>
    public Action<ILogger, ToolId, ToolCallId> Cancelled { get; }

    /// <summary>Gets the event logged when an invocation throws unexpectedly.</summary>
    public Action<ILogger, ToolId, ToolCallId, string> Faulted { get; }
}
