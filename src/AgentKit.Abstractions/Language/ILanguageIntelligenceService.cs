// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides bounded provider-neutral read-only language intelligence for one host-selected workspace profile.</summary>
public interface ILanguageIntelligenceService
{
    /// <summary>Gets the exact security audience that consumes language-observation grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Executes one already validated query and consumes its exact grant before protected observation.</summary>
    /// <param name="request">The query, bounds, and exact grant.</param>
    /// <param name="cancellationToken">Stops the caller's query wait.</param>
    /// <returns>The terminal bounded snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<LanguageQueryResult> QueryAsync(
        LanguageQueryRequest request,
        CancellationToken cancellationToken = default);
}
