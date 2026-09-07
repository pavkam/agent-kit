// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Records work resolved from one run scope for facade admission tests.</summary>
internal sealed class ScopedRecordingAgentLoop: IAgentLoop
{
    private readonly AdmissionRunEffects _effects;

    /// <summary>Initializes a scoped loop that records into <paramref name="effects"/>.</summary>
    /// <param name="effects">The non-null tracker shared by the test and each scoped loop instance.</param>
    public ScopedRecordingAgentLoop(AdmissionRunEffects effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _effects = effects;
        _effects.Scopes++;
    }

    /// <inheritdoc/>
    public Task<AgentLoopResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _effects.Requests.Add(request);
        return _effects.LoopException is { } exception
            ? Task.FromException<AgentLoopResult>(exception)
            : Task.FromResult(new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new AgentRunTurnLimitReached(request.MaxTurns),
            [],
            new SessionVersion(0)));
    }
}
