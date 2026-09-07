// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The candidate passed every validation stage and is accepted as application output.</summary>
public sealed record OutputAccepted: OutputProcessingResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputAccepted"/> record.</summary>
    /// <param name="output">The accepted, validated output value.</param>
    /// <param name="manifest">A record of how this candidate was processed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="manifest"/> is null.</exception>
    public OutputAccepted(ValidatedOutput output, OutputValidationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(manifest);

        Output = output;
        Manifest = manifest;
    }

    /// <summary>Gets the accepted, validated output value.</summary>
    public ValidatedOutput Output { get; init; }

    /// <summary>Gets a record of how this candidate was processed.</summary>
    public OutputValidationManifest Manifest { get; init; }
}
