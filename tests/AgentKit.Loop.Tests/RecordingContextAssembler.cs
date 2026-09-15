// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Records context requests before delegating to the real reduced assembler.</summary>
internal sealed class RecordingContextAssembler: IContextAssembler
{
    private readonly DefaultContextAssembler _inner = new();

    /// <summary>Gets requests in invocation order.</summary>
    public List<ContextAssemblyRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<ContextAssemblyResult> AssembleAsync(ContextAssemblyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);
        return _inner.AssembleAsync(request, cancellationToken);
    }
}
