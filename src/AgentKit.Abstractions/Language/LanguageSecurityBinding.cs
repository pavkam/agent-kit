// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates exact resources and secret-free fingerprints for language-intelligence authorization.</summary>
public static class LanguageSecurityBinding
{
    /// <summary>Returns the document or workspace resource observed by a query.</summary>
    /// <param name="kind">The requested language operation.</param>
    /// <param name="path">The document path for document-scoped operations.</param>
    /// <returns>One exact protected file or workspace-directory resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The path shape does not match <paramref name="kind"/>.</exception>
    public static ProtectedResource Resource(LanguageQueryKind kind, FileSystemPath? path)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        return kind == LanguageQueryKind.WorkspaceSymbols
            ? path.HasValue
                ? throw new ArgumentException("Workspace-symbol queries must omit the document path.", nameof(path))
                : new ProtectedResource(ProtectedResourceKind.Directory, ".")
            : path.HasValue
                ? new ProtectedResource(ProtectedResourceKind.File, path.Value.Value)
                : throw new ArgumentException("Document queries require a document path.", nameof(path));
    }

    /// <summary>Computes exact canonical authorization evidence for one language query.</summary>
    /// <param name="id">The query identity.</param>
    /// <param name="kind">The requested operation.</param>
    /// <param name="path">The optional document path.</param>
    /// <param name="position">The optional zero-based position.</param>
    /// <param name="query">The optional workspace-symbol query.</param>
    /// <param name="maximumResults">The positive result bound.</param>
    /// <param name="timeout">The finite query duration.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    public static InputFingerprint Fingerprint(
        LanguageQueryId id,
        LanguageQueryKind kind,
        FileSystemPath? path,
        LanguagePosition? position,
        string? query,
        int maximumResults,
        TimeSpan timeout)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "language-query-v1",
            id = id.ToString(),
            kind,
            path = path?.Value,
            line = position?.Line,
            character = position?.Character,
            queryFingerprint = query is null ? null : ProcessSecurityBinding.FingerprintText(query),
            maximumResults,
            timeoutTicks = timeout.Ticks,
        });
        return new InputFingerprint($"sha256:{Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }

    /// <summary>Computes the fingerprint carried by a validated request.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The exact request fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(LanguageQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Fingerprint(
            request.Id,
            request.Kind,
            request.Path,
            request.Position,
            request.Query,
            request.MaximumResults,
            request.Timeout);
    }
}
