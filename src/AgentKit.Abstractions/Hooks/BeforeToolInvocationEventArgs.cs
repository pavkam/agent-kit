// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text.Json;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.BeforeToolInvocation"/>: one tool call the model requested, before
/// schema validation, authorization, and invocation. Hooks may rewrite <see cref="Arguments"/> or set a
/// <see cref="Veto"/>; the call identity and tool reference are read-only.
/// </summary>
/// <remarks>
/// <para>
/// Rewritten arguments still pass the tool's schema validation and the security authority afterwards, so a hook
/// cannot bypass either. A veto short-circuits later hooks and produces a rejected terminal result carrying the
/// veto's safe reason; it cannot turn any other outcome into success.
/// </para>
/// <para>
/// This is a transform point: the loop dispatches it with <see cref="HookFailureMode.FailOperation"/>.
/// </para>
/// </remarks>
public sealed class BeforeToolInvocationEventArgs: AgentScopedHookEventArgs, IShortCircuitingHookArgs
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="call">The tool call part the model produced.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="call"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="dispatch"/>'s correlation names no turn.</exception>
    public BeforeToolInvocationEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId sessionId,
        ToolCallPart call)
        : base(dispatch, agentId, sessionId)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNotEqual(Correlation is InRunOperationCorrelation { TurnId: not null }, true, nameof(dispatch));
        Call = call;
        Arguments = call.Arguments;
    }

    /// <summary>Gets the run identity.</summary>
    /// <value>Read through the base <see cref="AgentHookEventArgs.Correlation"/>, which the constructor requires to be an in-run correlation; never a second stored copy.</value>
    public RunId RunId => ((InRunOperationCorrelation) Correlation).RunId;

    /// <summary>Gets the turn identity.</summary>
    /// <value>Read through the base <see cref="AgentHookEventArgs.Correlation"/>, which the constructor requires to name a turn; never a second stored copy.</value>
    public TurnId TurnId => ((InRunOperationCorrelation) Correlation).TurnId!.Value;

    /// <summary>Gets the model's tool call as committed; read-only.</summary>
    public ToolCallPart Call { get; }

    /// <summary>Gets the call identity.</summary>
    public ToolCallId CallId => Call.CallId;

    /// <summary>Gets the requested tool reference.</summary>
    public ToolReference Tool => Call.Tool;

    /// <summary>Gets or sets the arguments the tool will be invoked with.</summary>
    /// <value>Initially the model's arguments. A replacement must keep the original JSON kind, or be an object when the original was absent.</value>
    public JsonElement Arguments { get; set; }

    /// <summary>Gets or sets the veto that refuses the call, or <see langword="null"/> to let it proceed.</summary>
    public ToolInvocationVeto? Veto { get; set; }

    /// <summary>Gets whether a hook changed the arguments.</summary>
    public bool ArgumentsChanged => !JsonElement.DeepEquals(Arguments, Call.Arguments);

    /// <inheritdoc/>
    public bool IsShortCircuited => Veto is not null;

    /// <inheritdoc/>
    /// <exception cref="HookValidationException">
    /// The replacement arguments change the JSON kind of the original, or replace absent arguments with something
    /// other than an object.
    /// </exception>
    public override void Validate()
    {
        var original = Call.Arguments.ValueKind;
        var valid = original == JsonValueKind.Undefined
            ? Arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Object
            : Arguments.ValueKind == original;
        if (!valid)
        {
            throw new HookValidationException(
                $"A before-tool-invocation hook must keep the arguments a {original}; it produced {Arguments.ValueKind}.");
        }
    }

    /// <inheritdoc/>
    public override object? CaptureMutableState() => (Arguments, Veto);

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot)
    {
        if (snapshot is ValueTuple<JsonElement, ToolInvocationVeto?> captured)
        {
            Arguments = captured.Item1;
            Veto = captured.Item2;
        }
    }
}
