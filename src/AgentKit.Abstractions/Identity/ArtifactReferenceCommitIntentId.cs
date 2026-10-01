// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one caller-owned intent to commit an artifact reference into its owning store.</summary>
public readonly record struct ArtifactReferenceCommitIntentId
{
    /// <summary>Initializes a non-empty intent identity.</summary>
    /// <param name="value">The globally unique intent identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public ArtifactReferenceCommitIntentId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique intent identity.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form.</summary>
    public override string ToString() => Value.ToString("D");
}
