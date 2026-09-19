// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Discovers the hook registrations one source contributes to a profile.</summary>
/// <remarks>
/// Application registration extensions (for example <c>Add*Hook&lt;T&gt;</c>) and third-party features each
/// register their own source. <see cref="IHookCatalog"/> discovers from every registered source and merges the
/// results deterministically; registration order is preserved as the final tie-breaker after every ordering
/// constraint is resolved.
/// </remarks>
public interface IHookRegistrationSource
{
    /// <summary>Discovers this source's registrations for the requested profile.</summary>
    /// <param name="request">The profile being captured.</param>
    /// <param name="cancellationToken">Cancels discovery.</param>
    /// <returns>This source's discovered registrations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<HookRegistrationSnapshot> DiscoverAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken);
}
