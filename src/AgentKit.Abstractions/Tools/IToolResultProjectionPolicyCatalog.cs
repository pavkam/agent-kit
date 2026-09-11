// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves the exact retained projection policy named by an authoritative tool result.</summary>
/// <remarks>
/// Implementations support concurrent resolution and never substitute another key or revision. Resolution
/// supplies immutable policy evidence, not authority to transform content. Persistent implementations must
/// distinguish unavailable retained content from operational failure and keep revisions needed for recovery.
/// </remarks>
public interface IToolResultProjectionPolicyCatalog
{
    /// <summary>Resolves one captured policy reference without selecting a current or default revision.</summary>
    /// <param name="reference">The nonnull key and exact positive version captured with the terminal result.</param>
    /// <param name="cancellationToken">Cancels resolution; cancellation is propagated rather than reported as unavailable.</param>
    /// <returns>A resolved immutable snapshot with the requested reference, or an unavailable result retaining that reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cancels resolution.</exception>
    /// <remarks>Lookup failure must not trigger invocation, policy creation, or a lookup under a different reference.</remarks>
    public ValueTask<ToolResultProjectionPolicyResolution> ResolveAsync(
        ToolResultProjectionPolicyReference reference,
        CancellationToken cancellationToken);
}
