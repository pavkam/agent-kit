// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Counts attempted asynchronous reads and throws if startup invokes one.</summary>
internal sealed class ThrowingBootstrapTestSource: IAgentDefinitionSource
{
    /// <summary>Gets how many times the asynchronous source was read.</summary>
    public static int Reads { get; private set; }

    /// <inheritdoc/>
    public AgentDefinitionSourceId SourceId { get; } = new("test-source");

    /// <summary>Resets the static observation counter.</summary>
    public static void Reset() => Reads = 0;

    /// <inheritdoc/>
    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        Reads++;
        throw new InvalidOperationException("Build must not read this source.");
    }
}
