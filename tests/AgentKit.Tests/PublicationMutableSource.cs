// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

internal sealed class PublicationMutableSource(AgentDefinition definition): IAgentDefinitionSource
{
    public AgentDefinition Definition { get; set; } = definition;
    public AgentDefinitionSourceId SourceId { get; } = new("publication-mutable");
    public AgentDefinitionSourceId? ReturnedSourceId { get; set; }
    public bool PublishEmpty { get; set; }
    public bool ReturnNull { get; set; }
    public Action? AfterRead { get; set; }

    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReturnNull)
        {
            return ValueTask.FromResult<AgentDefinitionSourceSnapshot>(null!);
        }

        var snapshot = new AgentDefinitionSourceSnapshot(
            ReturnedSourceId ?? SourceId,
            new AgentDefinitionSourceVersion(1),
            0,
            PublishEmpty ? [] : [Definition]);
        AfterRead?.Invoke();
        return ValueTask.FromResult(snapshot);
    }
}

internal sealed class DefaultIdAgentDefinitionSource: IAgentDefinitionSource
{
    public AgentDefinitionSourceId SourceId => default;

    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
