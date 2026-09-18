// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.BeforeModelRequest"/>: the assembled provider request one turn
/// is about to send. Hooks may narrow <see cref="Settings"/>; everything else is read-only.
/// </summary>
/// <remarks>
/// <para>
/// The messages, tools, tool choice, and model are committed evidence of what the context assembler produced and
/// cannot be changed here. <see cref="Settings"/> starts as the request's settings; a hook may assign a replacement
/// as long as it does not raise <see cref="LlmRequestSettings.MaxOutputTokens"/> above the original cap, which
/// <see cref="Validate"/> enforces after every hook.
/// </para>
/// <para>
/// This is a transform point: the loop dispatches it with <see cref="HookFailureMode.FailOperation"/>, so a hook
/// failure fails the turn rather than sending a request whose settings are in an unknown state.
/// </para>
/// </remarks>
public sealed class BeforeModelRequestEventArgs: AgentHookEventArgs
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="correlation">The turn's in-run correlation, which names the turn.</param>
    /// <param name="timestamp">When the dispatch began.</param>
    /// <param name="invocationId">The dispatch's invocation identity.</param>
    /// <param name="turn">The one-based turn number within the run.</param>
    /// <param name="request">The assembled request context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turn"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="correlation"/> names no turn.</exception>
    public BeforeModelRequestEventArgs(
        AgentId agentId,
        SessionId sessionId,
        InRunOperationCorrelation correlation,
        DateTimeOffset timestamp,
        HookInvocationId invocationId,
        int turn,
        LlmRequestContext request)
        : base(agentId, sessionId, correlation, timestamp, invocationId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turn);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNotEqual(correlation.TurnId.HasValue, true, nameof(correlation));
        RunId = correlation.RunId;
        TurnId = correlation.TurnId!.Value;
        Turn = turn;
        Request = request;
        OriginalSettings = request.Settings;
        Settings = request.Settings;
    }

    /// <summary>Gets the run identity.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the turn identity.</summary>
    public TurnId TurnId { get; }

    /// <summary>Gets the one-based turn number within the run.</summary>
    public int Turn { get; }

    /// <summary>Gets the assembled request as the context assembler produced it; read-only.</summary>
    public LlmRequestContext Request { get; }

    /// <summary>Gets the settings the request carried before any hook ran.</summary>
    public LlmRequestSettings OriginalSettings { get; }

    /// <summary>Gets or sets the settings the request will be sent with.</summary>
    /// <value>Initially <see cref="OriginalSettings"/>.</value>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    public LlmRequestSettings Settings
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets whether a hook changed the settings.</summary>
    public bool SettingsChanged => !ReferenceEquals(Settings, OriginalSettings) && !Settings.Equals(OriginalSettings);

    /// <inheritdoc/>
    /// <exception cref="HookValidationException">
    /// The settings raise <see cref="LlmRequestSettings.MaxOutputTokens"/> above the original cap or remove it.
    /// </exception>
    public override void Validate()
    {
        if (OriginalSettings.MaxOutputTokens is { } cap && (Settings.MaxOutputTokens is null || Settings.MaxOutputTokens > cap))
        {
            throw new HookValidationException(
                $"A before-model-request hook may only narrow MaxOutputTokens; the request caps it at {cap}.");
        }
    }

    /// <inheritdoc/>
    public override object? CaptureMutableState() => Settings;

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot) => Settings = snapshot as LlmRequestSettings ?? OriginalSettings;
}
