// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Builds internally consistent, fully populated domain security and identity evidence for mirror round-trip cases.</summary>
/// <remarks>
/// <para>
/// Every sample uses fixed identities and instants so a case is deterministic and so two independently built samples compare
/// equal by the domain type's own structural equality. Samples are deliberately maximal: collections carry more than one
/// element in a meaningful order, and optional members are populated unless a case explicitly asks for their absence, so a
/// mirror that silently drops or reorders evidence fails rather than passing on a degenerate value.
/// </para>
/// <para>
/// This is a fixture factory, not a production component. It models a trusted ingress and an already-completed authority
/// decision; it never grants authority and is never composed into an engine.
/// </para>
/// </remarks>
internal static class TestEvidenceFactory
{
    /// <summary>Gets the fixed agent identity every sample scope binds to.</summary>
    /// <value>A non-empty agent identity stable across every sample in the suite.</value>
    internal static AgentId Agent { get; } = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    /// <summary>Gets the fixed session identity used whenever a sample scope belongs to a session.</summary>
    /// <value>A non-empty session identity stable across every sample in the suite.</value>
    internal static SessionId Session { get; } = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    /// <summary>Gets the base instant every sample timestamp is derived from.</summary>
    /// <value>A fixed instant, so no sample ever reads an ambient clock.</value>
    internal static DateTimeOffset Instant { get; } = new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>Creates a before-run correlation with or without its causing admission receipt.</summary>
    /// <param name="withAdmission">Whether the correlation records the admission that caused the operation.</param>
    /// <returns>A before-run correlation carrying no run identity.</returns>
    internal static BeforeRunOperationCorrelation BeforeRun(bool withAdmission) =>
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            withAdmission ? new AdmissionId(Guid.Parse("44444444-4444-4444-4444-444444444444")) : null);

    /// <summary>Creates an in-run correlation with or without its active turn identity.</summary>
    /// <param name="withTurn">Whether the correlation is turn-scoped rather than run-scoped.</param>
    /// <returns>An in-run correlation carrying a required run identity.</returns>
    internal static InRunOperationCorrelation InRun(bool withTurn) =>
        new InRunOperationCorrelation(
            new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new RunId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            withTurn ? new TurnId(Guid.Parse("66666666-6666-6666-6666-666666666666")) : null);

    /// <summary>Creates an after-run correlation naming its already-settled causal run.</summary>
    /// <returns>An after-run correlation carrying a required causal run identity.</returns>
    internal static AfterRunOperationCorrelation AfterRun() =>
        new AfterRunOperationCorrelation(
            new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new RunId(Guid.Parse("77777777-7777-7777-7777-777777777777")));

    /// <summary>Creates an authorization scope bound to the fixed agent and an in-run correlation.</summary>
    /// <param name="withSession">Whether the scope truthfully belongs to a session.</param>
    /// <returns>A scope whose optional session is present only when requested.</returns>
    internal static SecurityAuthorizationScope Scope(bool withSession = true) =>
        new SecurityAuthorizationScope(Agent, withSession ? Session : null, InRun(withTurn: true));

    /// <summary>Creates safe authentication evidence containing no credential material.</summary>
    /// <param name="withExpiry">Whether the evidence expires, in which case the expiry is strictly later than authentication.</param>
    /// <returns>Evidence whose optional expiry is present only when requested.</returns>
    internal static AuthenticationEvidence Evidence(bool withExpiry) =>
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("evidence-reference"),
            new IdentityIssuerId("issuer-primary"),
            "mutual-tls",
            Instant,
            withExpiry ? Instant.AddHours(2) : null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:evidence-fingerprint")));

    /// <summary>Creates the ordered claim set every sample identity and delegation link carries.</summary>
    /// <returns>Three claims covering each <see cref="IdentityClaimValueKind"/> in a meaningful order.</returns>
    /// <remarks>The order is part of the evidence, so a mirror that reorders claims must fail an ordering assertion.</remarks>
    internal static ImmutableArray<IdentityClaim> Claims() =>
    [
        new IdentityClaim(new IdentityIssuerId("issuer-primary"), "role", "operator", IdentityClaimValueKind.Text),
        new IdentityClaim(new IdentityIssuerId("issuer-secondary"), "mfa", "true", IdentityClaimValueKind.Boolean),
        new IdentityClaim(new IdentityIssuerId("issuer-primary"), "tier", "7", IdentityClaimValueKind.WholeNumber),
    ];

    /// <summary>Creates the first delegation ancestor of the sample identity.</summary>
    /// <returns>A same-tenant link whose assurance is high enough to remain an upper bound for the child identity.</returns>
    internal static DelegationIdentityLink FirstDelegationLink() =>
        new DelegationIdentityLink(
            new DelegationId(Guid.Parse("88888888-8888-8888-8888-888888888881")),
            new TenantId("tenant-primary"),
            new PrincipalId("principal-root"),
            new IdentityIssuerId("issuer-primary"),
            new AuthenticationEvidenceId("evidence-reference"),
            new IdentityVersion(1),
            Instant.AddMinutes(1),
            Claims(),
            IdentityAssuranceLevel.HardwareBacked);

    /// <summary>Creates the second delegation ancestor of the sample identity.</summary>
    /// <returns>A same-tenant link distinct from <see cref="FirstDelegationLink"/> in identity, principal, and version.</returns>
    internal static DelegationIdentityLink SecondDelegationLink() =>
        new DelegationIdentityLink(
            new DelegationId(Guid.Parse("88888888-8888-8888-8888-888888888882")),
            new TenantId("tenant-primary"),
            new PrincipalId("principal-intermediate"),
            new IdentityIssuerId("issuer-secondary"),
            new AuthenticationEvidenceId("evidence-reference"),
            new IdentityVersion(2),
            Instant.AddMinutes(2),
            [],
            IdentityAssuranceLevel.Strong);

    /// <summary>Creates a complete authenticated execution identity.</summary>
    /// <param name="withCollections">Whether the identity carries its ordered claims and delegation ancestry.</param>
    /// <returns>An identity whose assurance never exceeds any ancestor link's assurance.</returns>
    internal static ExecutionIdentity Identity(bool withCollections = true) =>
        new ExecutionIdentity(
            new TenantId("tenant-primary"),
            new PrincipalId("principal-leaf"),
            ExecutionSubjectKind.Service,
            Evidence(withExpiry: true),
            withCollections ? Claims() : [],
            withCollections ? [FirstDelegationLink(), SecondDelegationLink()] : [],
            IdentityAssuranceLevel.Basic,
            new IdentityVersion(3));

    /// <summary>Gets the policy version every sample grant and policy snapshot agrees on.</summary>
    /// <value>A positive version; the grant constructor requires the snapshot and the grant to match.</value>
    internal static SecurityPolicyVersion PolicyVersion { get; } = new SecurityPolicyVersion(7);

    /// <summary>Creates the immutable policy-snapshot reference captured by every sample authorization context.</summary>
    /// <returns>A reference whose version equals <see cref="PolicyVersion"/>.</returns>
    internal static SecurityPolicySnapshotReference PolicySnapshot() =>
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("99999999-9999-9999-9999-999999999999")),
            PolicyVersion,
            new ContentHash("sha256:policy-snapshot"));

    /// <summary>Creates captured authorization evidence consistent with one scope and identity.</summary>
    /// <param name="scope">The exact scope the enclosing evidence is bound to.</param>
    /// <param name="identity">The exact identity the enclosing evidence is bound to.</param>
    /// <returns>Authorization evidence the domain accepts alongside the same scope and identity.</returns>
    internal static SecurityAuthorizationContext Authorization(
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity) =>
        new SecurityAuthorizationContext(
            new SecurityProfileKey("profile-primary"),
            new SecurityProfileVersion(2),
            PolicySnapshot(),
            new ComponentKey<ISecurityAuthority>("authority-primary"),
            new AgentDefinitionRevision(0),
            new ConfigurationVersion(5),
            scope,
            identity);

    /// <summary>Creates the ordered canonical resources every sample bounded operation names.</summary>
    /// <returns>Three resources of distinct kinds in a meaningful order.</returns>
    internal static ImmutableArray<ProtectedResource> Resources() =>
    [
        new ProtectedResource(ProtectedResourceKind.File, "/workspace/first.txt"),
        new ProtectedResource(ProtectedResourceKind.Directory, "/workspace/nested"),
        new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://example.invalid/api"),
    ];

    /// <summary>Creates a bounded security grant.</summary>
    /// <param name="withAuthorization">Whether the grant retains the captured authorization context the authority evaluated.</param>
    /// <returns>A grant whose captured authorization is present exactly when requested.</returns>
    internal static SecurityGrant Grant(bool withAuthorization)
    {
        var scope = Scope();
        var identity = Identity();
        var id = new GrantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var requestId = new SecurityRequestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var audience = new ComponentId("agentkit.filesystem");
        var fingerprint = new InputFingerprint("sha256:grant-input");
        var revocationVersion = new SecurityRevocationVersion(4);
        return withAuthorization
            ? new SecurityGrant(
                id,
                requestId,
                scope,
                identity,
                Authorization(scope, identity),
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.CreateOrReplace,
                Resources(),
                fingerprint,
                PolicyVersion,
                revocationVersion,
                Instant,
                Instant.AddMinutes(30),
                3)
            : new SecurityGrant(
                id,
                requestId,
                scope,
                identity,
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.CreateOrReplace,
                Resources(),
                fingerprint,
                PolicyVersion,
                revocationVersion,
                Instant,
                Instant.AddMinutes(30),
                3);
    }

    /// <summary>Creates one fully normalized protected request.</summary>
    /// <param name="withAuthorization">Whether the request carries the captured authorization evidence to evaluate.</param>
    /// <param name="withToolCall">Whether a tool call caused the request.</param>
    /// <returns>A request whose optional members are present exactly when requested.</returns>
    internal static SecurityRequest Request(bool withAuthorization, bool withToolCall)
    {
        var scope = Scope();
        var identity = Identity();
        var id = new SecurityRequestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        ToolCallId? toolCallId =
            withToolCall ? new ToolCallId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")) : null;
        var audience = new ComponentId("agentkit.filesystem");
        var fingerprint = new InputFingerprint("sha256:request-input");
        return withAuthorization
            ? new SecurityRequest(
                id,
                scope,
                toolCallId,
                identity,
                Authorization(scope, identity),
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.Append,
                Resources(),
                fingerprint,
                Instant.AddMinutes(10),
                4)
            : new SecurityRequest(
                id,
                scope,
                toolCallId,
                identity,
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.Append,
                Resources(),
                fingerprint,
                Instant.AddMinutes(10),
                4);
    }

    /// <summary>Creates fresh concrete enforcement evidence for one about-to-happen effect.</summary>
    /// <param name="withAuthorization">Whether the effecting boundary presented captured authorization evidence.</param>
    /// <returns>Enforcement evidence whose captured authorization is present exactly when requested.</returns>
    internal static SecurityEnforcementRequest Enforcement(bool withAuthorization)
    {
        var scope = Scope();
        var identity = Identity();
        var audience = new ComponentId("agentkit.filesystem");
        var fingerprint = new InputFingerprint("sha256:enforcement-input");
        var revocationVersion = new SecurityRevocationVersion(4);
        return withAuthorization
            ? new SecurityEnforcementRequest(
                scope,
                identity,
                Authorization(scope, identity),
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.Replace,
                Resources(),
                fingerprint,
                revocationVersion)
            : new SecurityEnforcementRequest(
                scope,
                identity,
                audience,
                SecurityOperationKind.FileWrite,
                SecurityEffect.Replace,
                Resources(),
                fingerprint,
                revocationVersion);
    }

    /// <summary>Creates the durable proof that a grant store consumed one use.</summary>
    /// <param name="withFence">Whether the effect required a distributed ownership fence.</param>
    /// <returns>A receipt whose optional fence is present exactly when requested.</returns>
    internal static SecurityEnforcementIntentReceipt Receipt(bool withFence) =>
        new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            new GrantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new SecurityRequestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            Enforcement(withAuthorization: true),
            withFence ? new FencingToken(42) : null,
            new ContentHash("sha256:effect-fingerprint"),
            Instant.AddMinutes(1));
}
