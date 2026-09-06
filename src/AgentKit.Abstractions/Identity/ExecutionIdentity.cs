// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable identity of who is acting for one operation, normalized at
/// a trusted ingress and propagated through session, security, and
/// compaction operations.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>ExecutionIdentity</c> described by the execution-identity-and-tenancy
/// architecture, which additionally carries authentication evidence, claims,
/// a delegation chain, and an assurance level produced by the
/// not-yet-implemented <c>AgentKit.Identity</c> package. Once that package
/// exists, session and security components will consume its richer identity
/// type instead; this type exists so <see cref="SessionOperationContext"/>
/// and compaction operations have a stable, self-contained identity shape to
/// depend on in the meantime. It preserves the two pieces of identity every
/// downstream consumer already depends on — <see cref="TenantId"/> and
/// <see cref="PrincipalId"/> — so authorization and audit remain meaningful.
/// </para>
/// <para>
/// Identity never grants authority by itself: every protected operation
/// still evaluates this identity against the configured security policy
/// before proceeding.
/// </para>
/// </remarks>
public sealed record ExecutionIdentity
{
    /// <summary>Initializes a new instance of the <see cref="ExecutionIdentity"/> record.</summary>
    /// <param name="tenantId">The tenant on whose behalf the operation is performed.</param>
    /// <param name="principalId">The authenticated principal performing the operation.</param>
    /// <param name="subjectKind">The kind of subject this identity represents.</param>
    /// <param name="extensions">Ingress-specific or forward-compatible identity data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ExecutionIdentity(
        TenantId tenantId,
        PrincipalId principalId,
        ExecutionSubjectKind subjectKind,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        TenantId = tenantId;
        PrincipalId = principalId;
        SubjectKind = subjectKind;
        Extensions = extensions;
    }

    /// <summary>Gets the tenant on whose behalf the operation is performed.</summary>
    public TenantId TenantId { get; init; }

    /// <summary>Gets the authenticated principal performing the operation.</summary>
    public PrincipalId PrincipalId { get; init; }

    /// <summary>Gets the kind of subject this identity represents.</summary>
    public ExecutionSubjectKind SubjectKind { get; init; }

    /// <summary>Gets ingress-specific or forward-compatible identity data.</summary>
    public ExtensionData Extensions { get; init; }
}
