// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IOutputProcessor"/> test double that throws <see cref="NotSupportedException"/> when processing is requested.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted loop never validates terminal output.</remarks>
public sealed class UnsupportedOutputProcessor: IOutputProcessor
{
    /// <inheritdoc/>
    public ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support output processing.");
}
