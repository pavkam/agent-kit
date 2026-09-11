// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains a policy-owned halt reason and safe provenance without granting authority.</summary>
/// <remarks>The immutable evidence is descriptive; its owner is responsible for safe normalization and actual lifecycle transitions.</remarks>
public sealed record PolicyHalt
{
    /// <summary>Captures a nonnull normalized reason without performing further work.</summary>
    /// <param name="reason">The nonnull immutable evidence to preserve.</param>
    /// <exception cref="ArgumentNullException">The evidence is null.</exception>
    public PolicyHalt(AgentError reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Error = reason;
    }
    /// <summary>Gets the original typed evidence without replacing its correlation or effect certainty.</summary>
    /// <value>The nonnull immutable reason captured at construction.</value>
    public AgentError Error { get; }
}
