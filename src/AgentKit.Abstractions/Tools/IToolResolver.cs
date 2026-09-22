// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one raw call's requested provider alias against an exact captured tool catalog.</summary>
/// <remarks>
/// Resolution validates the request's catalog version and resolves the alias only through the exact catalog
/// snapshot captured for the call. It neither validates arguments, authorizes invocation, nor
/// invokes the tool. Resolution success transfers both a <see cref="ResolvedToolCall"/> and its owned invoker
/// lease to the caller; failure or cancellation releases any partial acquisition before returning or propagating.
/// </remarks>
public interface IToolResolver
{
    /// <summary>Resolves one raw call against the exact captured catalog.</summary>
    /// <param name="capture">The retained catalog capture the call resolves against.</param>
    /// <param name="request">The raw provider-emitted call request to resolve.</param>
    /// <param name="cancellationToken">Signals cancellation before a successful resolution is returned.</param>
    /// <returns>The closed resolved or unresolved outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> or <paramref name="request"/> is null.</exception>
    public ValueTask<ToolResolutionResult> ResolveAsync(
        IToolCatalogCapture capture,
        ToolCallRequest request,
        CancellationToken cancellationToken = default);
}
