// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies the live revocation epoch observed at grant issue, approval binding, and consumption.</summary>
/// <remarks>
/// The authority reads this generation on every evaluation path that mints or validates bounded authority. A raised
/// generation invalidates grants and approval bindings captured under an earlier epoch without rewriting historical audit
/// evidence.
/// </remarks>
public interface ISecurityRevocationGeneration
{
    /// <summary>Gets the current revocation generation.</summary>
    /// <param name="cancellationToken">Cancels before the generation is returned.</param>
    /// <returns>The positive revocation epoch effective for new grants and consumption checks.</returns>
    public ValueTask<SecurityRevocationVersion> GetCurrentAsync(CancellationToken cancellationToken = default);
}
