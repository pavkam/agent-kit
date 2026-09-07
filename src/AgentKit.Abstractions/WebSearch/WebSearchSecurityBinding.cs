// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;

/// <summary>Produces canonical security evidence for provider-backed web search.</summary>
public static class WebSearchSecurityBinding
{
    /// <summary>Fingerprints all behaviorally meaningful inputs of one search attempt.</summary>
    /// <param name="requestId">The attempt identity.</param>
    /// <param name="providerId">The selected provider.</param>
    /// <param name="destination">The configured secret-free destination.</param>
    /// <param name="query">The exact classified query text.</param>
    /// <param name="domains">The ordered domain allow-filter.</param>
    /// <param name="freshness">The requested recency.</param>
    /// <param name="maximumResults">The result ceiling.</param>
    /// <param name="deadline">The exclusive attempt deadline.</param>
    /// <returns>A deterministic SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is blank or <paramref name="domains"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or result bound is invalid.</exception>
    public static InputFingerprint Fingerprint(
        WebSearchRequestId requestId,
        ProviderId providerId,
        ProtectedResource destination,
        string query,
        ImmutableArray<NormalizedHost> domains,
        WebSearchFreshness freshness,
        int maximumResults,
        DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfDefault(domains);
        ArgumentOutOfRangeException.ThrowIfUndefined(freshness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            requestId = requestId.ToString(),
            providerId = providerId.Value,
            destination = new { kind = destination.Kind.ToString(), destination.Identifier },
            query,
            domains = domains.Select(static domain => domain.Value),
            freshness = freshness.ToString(),
            maximumResults,
            deadline = deadline.ToUniversalTime().ToString("O"),
        });
        return new InputFingerprint(Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
