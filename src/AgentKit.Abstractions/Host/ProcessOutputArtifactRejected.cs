// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that complete output could not be preserved without changing process settlement.</summary>
public sealed record ProcessOutputArtifactRejected: ProcessOutputArtifactResult
{
    /// <summary>Initializes a safe preservation rejection.</summary>
    /// <param name="safeMessage">The non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public ProcessOutputArtifactRejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; }
}
