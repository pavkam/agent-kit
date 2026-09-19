// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One content-free observation of a single hook invocation's outcome.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. The kernel publishes one instance per invocation to every registered
/// <see cref="IHookDiagnosticSink"/> through <see cref="IHookDiagnosticDispatcher"/>, whether the invocation
/// succeeded, failed, was isolated, or was rejected before execution.
/// </para>
/// <para>
/// This diagnostic never carries event-argument content, exception messages, or tool arguments/results: only
/// identity, timing, and a normalized outcome classification. Sinks that need more must correlate this
/// diagnostic's identities against a separately governed, explicitly classified content-capture channel.
/// </para>
/// </remarks>
public sealed record HookInvocationDiagnostic
{
    /// <summary>Initializes a new instance of the <see cref="HookInvocationDiagnostic"/> record.</summary>
    /// <param name="point">The hook point that was dispatched.</param>
    /// <param name="invocation">The invocation this diagnostic describes.</param>
    /// <param name="outcome">The normalized outcome of this invocation.</param>
    /// <param name="startedAt">When this invocation began.</param>
    /// <param name="duration">How long this invocation ran before completing, failing, or being isolated.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is default, <paramref name="outcome"/> is not a defined value, or
    /// <paramref name="duration"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="invocation"/> is null.</exception>
    public HookInvocationDiagnostic(
        HookPointId point,
        HookInvocationContext invocation,
        HookInvocationOutcome outcome,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        Point = point;
        Invocation = invocation;
        Outcome = outcome;
        StartedAt = startedAt;
        Duration = duration;
    }

    /// <summary>Gets the hook point that was dispatched.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the invocation this diagnostic describes.</summary>
    public HookInvocationContext Invocation { get; }

    /// <summary>Gets the normalized outcome of this invocation.</summary>
    public HookInvocationOutcome Outcome { get; }

    /// <summary>Gets when this invocation began.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets how long this invocation ran before completing, failing, or being isolated.</summary>
    public TimeSpan Duration { get; }
}
