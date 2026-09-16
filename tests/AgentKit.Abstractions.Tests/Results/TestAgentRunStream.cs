// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

/// <summary>Supplies immutable identity evidence without implementing real streaming behavior.</summary>
internal sealed class TestAgentRunStream: IAgentRunStream<string>
{
    /// <inheritdoc/>
    public AgentId AgentId => TestSupport.RunResultTestData.Agent;

    /// <inheritdoc/>
    public SessionId SessionId => TestSupport.RunResultTestData.Session;

    /// <inheritdoc/>
    public ConversationId? ConversationId => null;

    /// <inheritdoc/>
    public RunId RunId => TestSupport.RunResultTestData.Run;

    /// <inheritdoc/>
    public IAsyncEnumerable<RunEvent> ReadAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<AgentRunFinished<string>> Completion => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
