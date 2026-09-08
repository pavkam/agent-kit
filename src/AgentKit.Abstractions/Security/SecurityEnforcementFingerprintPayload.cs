// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Combines complete enforcement and intent evidence for one canonical private digest.</summary>
internal sealed record SecurityEnforcementFingerprintPayload
{
    /// <summary>Initializes the immutable canonical payload.</summary>
    /// <param name="enforcement">The complete recomputed protected-effect evidence.</param>
    /// <param name="intent">The stable consumption identity and required fence.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    internal SecurityEnforcementFingerprintPayload(
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        Enforcement = enforcement;
        Intent = intent;
    }

    /// <summary>Gets the recomputed concrete protected effect.</summary>
    /// <value>The complete immutable scope, identity, audience, resource, content, and revocation evidence.</value>
    public SecurityEnforcementRequest Enforcement { get; }

    /// <summary>Gets the stable grant-consumption intent.</summary>
    /// <value>The non-default attempt identity and its exact optional ownership fence.</value>
    public SecurityEnforcementIntent Intent { get; }
}
