// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>
/// Deterministic builders and fakes for engine composition tests.
/// </summary>
internal static class CompositionTestData
{
    public static AgentId AgentId { get; } =
        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static SessionId SessionId { get; } =
        new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));

    public static BranchId BranchId { get; } =
        new(Guid.Parse("c0000000-0000-0000-0000-000000000003"));

    public static ExecutionIdentity Identity() =>
        new(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, ExtensionData.Empty);

    public static AgentDefinition Definition(
        AgentId? id = null,
        string displayName = "test agent",
        int maxTurns = 8,
        long revision = 1) =>
        new(
            id ?? AgentId,
            new AgentDefinitionRevision(revision),
            displayName,
            new ModelSelectionPolicy([new ModelAlias("chat")]),
            ModelRequirements.None,
            instructions: [],
            tools: [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            new RunPolicyDefaults(maxTurns, TimeSpan.FromMinutes(1)),
            ExtensionData.Empty);

    public static AgentRunOptions RunOptions(int? maxTurns = null, TimeSpan? attemptTimeout = null) =>
        new(SessionId, BranchId, Identity(), maxTurns, attemptTimeout);

    /// <summary>
    /// Builds a composition that satisfies engine validation: the facade
    /// defaults, one recording loop, and one published agent.
    /// </summary>
    public static AgentEngineBuilder RunnableBuilder(
        RecordingAgentLoop? loop = null,
        AgentDefinition? definition = null)
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddSingleton<IAgentLoop>(loop ?? new RecordingAgentLoop());
        _ = builder.Services.AddAgent(definition ?? Definition());
        return builder;
    }
}

/// <summary>
/// An <see cref="IAgentLoop"/> that records the request it received and
/// returns a completed run without contacting anything.
/// </summary>
internal sealed class RecordingAgentLoop: IAgentLoop
{
    /// <summary>Gets every request this loop received, in call order.</summary>
    public List<AgentRunRequest> ReceivedRequests { get; } = [];

    public Task<AgentLoopResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ReceivedRequests.Add(request);

        return Task.FromResult(new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new AgentRunTurnLimitReached(request.MaxTurns),
            [],
            new SessionVersion(0)));
    }
}

/// <summary>
/// A definition source that publishes a fixed set at a chosen precedence.
/// </summary>
internal sealed class FakeAgentDefinitionSource(
    string sourceId,
    int precedence,
    params AgentDefinition[] definitions): IAgentDefinitionSource
{
    public AgentDefinitionSourceId SourceId { get; } = new(sourceId);

    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new AgentDefinitionSourceSnapshot(
            SourceId,
            new AgentDefinitionSourceVersion(1),
            precedence,
            [.. definitions]));
    }
}
