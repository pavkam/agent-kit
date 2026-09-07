// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one exact bounded read-only language-intelligence operation under an enforceable grant.</summary>
public sealed record LanguageQueryRequest
{
    /// <summary>Initializes a validated portable query.</summary>
    /// <param name="id">The stable query identity.</param>
    /// <param name="kind">The requested operation.</param>
    /// <param name="path">The document path required by document-scoped operations.</param>
    /// <param name="position">The position required by hover and relationship operations.</param>
    /// <param name="query">The text required by workspace-symbol search.</param>
    /// <param name="maximumResults">The positive retained result bound.</param>
    /// <param name="timeout">The finite positive total query duration.</param>
    /// <param name="grant">The exact single-use observation grant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentException">The operation-specific path, position, or query shape is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity, enum, result bound, or timeout is invalid.</exception>
    public LanguageQueryRequest(
        LanguageQueryId id,
        LanguageQueryKind kind,
        FileSystemPath? path,
        LanguagePosition? position,
        string? query,
        int maximumResults,
        TimeSpan timeout,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(grant);
        ValidateShape(kind, path, position, query);
        Id = id;
        Kind = kind;
        Path = path;
        Position = position;
        Query = query;
        MaximumResults = maximumResults;
        Timeout = timeout;
        Grant = grant;
    }

    /// <summary>Gets the stable query identity.</summary>
    public LanguageQueryId Id { get; }
    /// <summary>Gets the requested operation.</summary>
    public LanguageQueryKind Kind { get; }
    /// <summary>Gets the document path for document-scoped operations.</summary>
    public FileSystemPath? Path { get; }
    /// <summary>Gets the zero-based position for position-scoped operations.</summary>
    public LanguagePosition? Position { get; }
    /// <summary>Gets the workspace-symbol query when applicable.</summary>
    public string? Query { get; }
    /// <summary>Gets the maximum retained result count.</summary>
    public int MaximumResults { get; }
    /// <summary>Gets the finite total query duration.</summary>
    public TimeSpan Timeout { get; }
    /// <summary>Gets the exact observation grant consumed by the provider.</summary>
    public SecurityGrant Grant { get; }

    private static void ValidateShape(
        LanguageQueryKind kind,
        FileSystemPath? path,
        LanguagePosition? position,
        string? query)
    {
        var positionRequired = kind is LanguageQueryKind.Hover
            or LanguageQueryKind.Definitions
            or LanguageQueryKind.Implementations
            or LanguageQueryKind.References;
        var pathRequired = kind != LanguageQueryKind.WorkspaceSymbols;
        if (pathRequired != path.HasValue
            || positionRequired != position.HasValue
            || kind == LanguageQueryKind.WorkspaceSymbols != !string.IsNullOrWhiteSpace(query)
            || (kind != LanguageQueryKind.WorkspaceSymbols && query is not null))
        {
            throw new ArgumentException("The path, position, and query do not match the requested language operation.", nameof(kind));
        }
    }
}
