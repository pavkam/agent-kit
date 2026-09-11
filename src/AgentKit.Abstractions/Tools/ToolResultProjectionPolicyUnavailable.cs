// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the exact captured projection-policy snapshot is unavailable.</summary>
/// <remarks>Callers retain the terminal result for recovery; this outcome never permits using another revision or repeating the tool effect.</remarks>
public sealed record ToolResultProjectionPolicyUnavailable: ToolResultProjectionPolicyResolution
{
    /// <summary>Initializes an unavailable outcome retaining the requested policy reference.</summary>
    /// <param name="reference">The nonnull exact key and version that could not be resolved.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ToolResultProjectionPolicyUnavailable(ToolResultProjectionPolicyReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <inheritdoc/>
    public override ToolResultProjectionPolicyReference Reference { get; }
}
