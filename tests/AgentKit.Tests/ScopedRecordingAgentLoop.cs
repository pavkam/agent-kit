// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.TestSupport;

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
    public Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();
        _effects.Requests.Add(request);
        return _effects.LoopException is { } exception
            ? Task.FromException<AgentLoopResult>(exception)
            : CompleteAsync(request, services, cancellationToken);
    }

    private static async Task<AgentLoopResult> CompleteAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken)
    {
        var version = await CompositionTestData.CurrentSessionVersionAsync(request, services, cancellationToken)
            .ConfigureAwait(false);
        return new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new RunPolicyHalted(new PolicyHalt(RunResultTestData.Error(AgentErrorCodes.RequestLimit))),
            [],
            version,
            null,
            new RunUsage(request.RunId, []),
            new RunSettlementCompleted());
    }
}
