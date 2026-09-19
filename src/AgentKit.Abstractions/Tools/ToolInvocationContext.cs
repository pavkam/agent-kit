// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>The restricted immutable context an <see cref="IToolInvoker"/> receives for one already validated and authorized attempt.</summary>
/// <remarks>
/// This context intentionally excludes the loop, the dependency container, raw credentials unrelated to the tool,
/// and permission to append arbitrary messages. It carries only the final canonical arguments, the approved
/// resource scope as a bounded <see cref="SecurityGrant"/>, identity, deadline, attempt count, and a bounded
/// progress reporter. Ship note: this interim shape omits the optional <c>SessionProfileSnapshot</c> the tool-call
/// lifecycle contract allows for session-backed tools; that field lands with the session-aware executor pipeline.
/// This type is an immutable value object with structural equality over its fields and is safe to share across
/// threads without synchronization.
/// </remarks>
public sealed record ToolInvocationContext
{
    /// <summary>Initializes an immutable tool-invocation context.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param>
    /// <param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param>
    /// <param name="callId">The nondefault provider-correlated call identity.</param>
    /// <param name="tool">The nonnull resolved tool descriptor.</param>
    /// <param name="toolVersion">The nondefault resolved version, equal to <paramref name="tool"/>'s declared version.</param>
    /// <param name="arguments">The final, already validated canonical arguments.</param>
    /// <param name="invocationGrant">The nonnull bounded grant approving this exact invocation's resource scope.</param>
    /// <param name="attempt">The positive attempt count, starting at one for the first attempt.</param>
    /// <param name="requestedAt">The original request timestamp.</param>
    /// <param name="invocationStartedAt">The timestamp this attempt started.</param>
    /// <param name="deadline">The instant by which this attempt must settle.</param>
    /// <param name="progress">The nonnull bounded live-progress reporter for this attempt.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An identity or <paramref name="toolVersion"/> is default, or <paramref name="attempt"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tool"/>, <paramref name="invocationGrant"/>, or <paramref name="progress"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="toolVersion"/> does not equal <paramref name="tool"/>'s declared version, or
    /// <paramref name="invocationGrant"/>'s scope does not match the supplied agent/session/run/turn/operation.
    /// </exception>
    public ToolInvocationContext(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        ToolCallId callId,
        ToolDescriptor tool,
        ToolVersion toolVersion,
        JsonElement arguments,
        SecurityGrant invocationGrant,
        int attempt,
        DateTimeOffset requestedAt,
        DateTimeOffset invocationStartedAt,
        DateTimeOffset deadline,
        IToolProgressReporter progress)
    {
        AcceptedToolCall.ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        ArgumentException.ThrowIfNotEqual(tool.Version, toolVersion, nameof(toolVersion));
        ArgumentNullException.ThrowIfNull(invocationGrant);
        ValidateGrantScope(agentId, sessionId, runId, turnId, operationId, invocationGrant);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);
        ArgumentNullException.ThrowIfNull(progress);

        CallId = callId;
        Tool = tool;
        ToolVersion = toolVersion;
        Arguments = arguments;
        InvocationGrant = invocationGrant;
        Attempt = attempt;
        RequestedAt = requestedAt;
        InvocationStartedAt = invocationStartedAt;
        Deadline = deadline;
        Progress = progress;
    }

    /// <summary>Gets the owning agent.</summary>
    /// <value>Read through <see cref="InvocationGrant"/>'s bound scope, which the constructor validates against the supplied identity; never a second stored copy.</value>
    public AgentId AgentId => InvocationGrant.Scope.AgentId;

    /// <summary>Gets the owning session.</summary>
    /// <value>Read through <see cref="InvocationGrant"/>'s bound scope, which the constructor requires to be present and equal to the supplied identity; never a second stored copy.</value>
    public SessionId SessionId => InvocationGrant.Scope.SessionId!.Value;

    /// <summary>Gets the active run.</summary>
    /// <value>Read through <see cref="InvocationGrant"/>'s bound in-run correlation; never a second stored copy.</value>
    public RunId RunId => ((InRunOperationCorrelation) InvocationGrant.Scope.Correlation).RunId;

    /// <summary>Gets the active turn.</summary>
    /// <value>Read through <see cref="InvocationGrant"/>'s bound in-run correlation; never a second stored copy.</value>
    public TurnId TurnId => ((InRunOperationCorrelation) InvocationGrant.Scope.Correlation).TurnId!.Value;

    /// <summary>Gets the causal operation.</summary>
    /// <value>Read through <see cref="InvocationGrant"/>'s bound in-run correlation; never a second stored copy.</value>
    public OperationId OperationId => ((InRunOperationCorrelation) InvocationGrant.Scope.Correlation).OperationId;

    /// <summary>Gets the provider-correlated call identity.</summary>
    /// <value>A nondefault identity that also identifies this call's <see cref="ToolCallPart"/> and terminal <see cref="ToolResultPart"/>.</value>
    public ToolCallId CallId { get; }

    /// <summary>Gets the resolved tool descriptor.</summary>
    /// <value>The exact captured descriptor selected by resolution; untrusted metadata about the tool, not authority.</value>
    public ToolDescriptor Tool { get; }

    /// <summary>Gets the resolved tool version.</summary>
    /// <value>A nondefault version equal to <see cref="Tool"/>'s declared version.</value>
    public ToolVersion ToolVersion { get; }

    /// <summary>Gets the final canonical arguments.</summary>
    /// <value>Already bounded, parsed, schema-validated, and authorized arguments.</value>
    public JsonElement Arguments { get; }

    /// <summary>Gets the bounded grant approving this exact invocation.</summary>
    /// <value>Single-use evidence the invoker never inspects for reauthorization; the effecting boundary validates it again.</value>
    public SecurityGrant InvocationGrant { get; }

    /// <summary>Gets the attempt count for this invocation.</summary>
    /// <value>A positive count starting at one for the first attempt.</value>
    public int Attempt { get; }

    /// <summary>Gets the original request timestamp.</summary>
    /// <value>A timestamp fact without ordering inference against other timestamps, since clocks can move backward.</value>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Gets the timestamp this attempt started.</summary>
    /// <value>A timestamp fact recorded when the invoker began this attempt.</value>
    public DateTimeOffset InvocationStartedAt { get; }

    /// <summary>Gets the instant by which this attempt must settle.</summary>
    /// <value>An advisory bound the invoker should honor; enforcement remains a scheduler/executor responsibility.</value>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets the bounded live-progress reporter for this attempt.</summary>
    /// <value>A reporter fenced once the outcome is staged.</value>
    public IToolProgressReporter Progress { get; }

    /// <summary>Determines complete structural context equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every scalar/reference field is equal.</returns>
    public bool Equals(ToolInvocationContext? other) =>
        other is not null
        && CallId == other.CallId
        && Tool == other.Tool
        && ToolVersion == other.ToolVersion
        && Arguments.GetRawText() == other.Arguments.GetRawText()
        && InvocationGrant == other.InvocationGrant
        && Attempt == other.Attempt
        && RequestedAt == other.RequestedAt
        && InvocationStartedAt == other.InvocationStartedAt
        && Deadline == other.Deadline
        && ReferenceEquals(Progress, other.Progress);

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every scalar/reference field.</returns>
    public override int GetHashCode() => HashCode.Combine(
        CallId, Tool, ToolVersion, InvocationGrant, Attempt, RequestedAt, InvocationStartedAt, Deadline);

    /// <summary>Validates that a grant's scope is bound to the supplied identity and in-run correlation.</summary>
    /// <param name="agentId">The expected agent.</param>
    /// <param name="sessionId">The expected session.</param>
    /// <param name="runId">The expected run.</param>
    /// <param name="turnId">The expected turn.</param>
    /// <param name="operationId">The expected operation.</param>
    /// <param name="invocationGrant">The grant whose scope must match exactly.</param>
    /// <exception cref="ArgumentException">The scope's agent, session, or in-run correlation does not match.</exception>
    private static void ValidateGrantScope(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        SecurityGrant invocationGrant)
    {
        var scope = invocationGrant.Scope;
        ArgumentException.ThrowIfNotEqual(scope.AgentId, agentId, nameof(invocationGrant));
        ArgumentException.ThrowIfNotEqual(scope.SessionId, sessionId, nameof(invocationGrant));
        var correlation = scope.Correlation as InRunOperationCorrelation;
        ArgumentException.ThrowIfNotEqual(correlation is not null, true, nameof(invocationGrant));
        Debug.Assert(correlation is not null, "The prior check guarantees an in-run correlation.");
        ArgumentException.ThrowIfNotEqual(correlation.RunId, runId, nameof(invocationGrant));
        ArgumentException.ThrowIfNotEqual(correlation.TurnId, turnId, nameof(invocationGrant));
        ArgumentException.ThrowIfNotEqual(correlation.OperationId, operationId, nameof(invocationGrant));
    }
}
