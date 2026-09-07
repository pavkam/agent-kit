// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests durable preservation of one complete process output stream after execution settles.</summary>
public sealed record ProcessOutputArtifactRequest
{
    /// <summary>Initializes a complete output preservation request.</summary>
    /// <param name="intent">The exact executed process intent.</param>
    /// <param name="scope">The execution security scope.</param>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="kind">The distinct output stream.</param>
    /// <param name="content">The complete bounded bytes.</param>
    /// <param name="idempotencyKey">The stable output-specific replay key.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException">Bytes are default or the replay key is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public ProcessOutputArtifactRequest(ResolvedProcessIntent intent, SecurityAuthorizationScope scope, ExecutionIdentity identity, ProcessOutputKind kind, ImmutableArray<byte> content, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfDefault(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Intent = intent;
        Scope = scope;
        Identity = identity;
        Kind = kind;
        Content = content;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact executed process intent.</summary>
    public ResolvedProcessIntent Intent { get; }
    /// <summary>Gets the execution security scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated execution identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the distinct output stream.</summary>
    public ProcessOutputKind Kind { get; }
    /// <summary>Gets the complete bounded bytes.</summary>
    public ImmutableArray<byte> Content { get; }
    /// <summary>Gets the stable output-specific replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
