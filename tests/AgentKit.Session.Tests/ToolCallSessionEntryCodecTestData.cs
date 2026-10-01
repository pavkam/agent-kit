// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>Deterministic accepted and terminal tool-call session entries shared by the tool-call codec fixtures.</summary>
internal static class ToolCallSessionEntryCodecTestData
{
    internal static readonly DateTimeOffset RecordedAt = new(1970, 1, 1, 1, 30, 0, TimeSpan.FromMinutes(90));

    internal static AcceptedToolCall Accepted(
        ToolEffect effect = ToolEffect.Mutating,
        IdempotencyClassification? idempotency = IdempotencyClassification.IdempotentWithKey,
        string alias = "provider-tool")
    {
        var authorization = Authorization();
        var normalization = Normalization(execution: new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)));
        return new AcceptedToolCall(
            AgentId, SessionId, RunId, TurnId, OperationId, CallId, authorization,
            new ToolCallAcceptanceEvidence(new GrantId(Id(60)), new InputFingerprint("sha256:validated"), RecordedAt),
            new ToolAlias(alias), new ToolId("tool"), new ToolVersion("1.2"),
            new ToolEffects(effect, idempotency, [ProtectedResourceKind.File, ProtectedResourceKind.Process]),
            idempotency is IdempotencyClassification.IdempotentWithKey ? new IdempotencyKey("key-1") : null,
            new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog-1"), 2, new InputFingerprint("sha256:raw")),
            normalization, normalization.ProjectionPolicy, RecordedAt);
    }

    internal static ToolCallAcceptedSessionEntry AcceptedEntry(AcceptedToolCall? call = null)
    {
        var accepted = call ?? Accepted();
        return new ToolCallAcceptedSessionEntry(
            new SessionEntryId(Id(70)), Address, Correlation, BranchId, new SessionSequence(12), new SessionEntryId(Id(71)),
            RecordedAt, new SchemaVersion("1"), accepted);
    }

    internal static ToolCallResult Terminal(
        bool accepted = true,
        ToolTerminalStatus status = ToolTerminalStatus.InvocationFailed,
        ToolError? error = null,
        ToolUsage? usage = null,
        ExtensionData? extensions = null,
        ImmutableArray<ToolResultContent>? content = null)
    {
        var call = Accepted();
        var authorization = call.Authorization;
        var normalization = accepted ? call.Normalization : Normalization(execution: null);
        return new ToolCallResult(
            AgentId, SessionId, RunId, TurnId, OperationId, CallId, authorization,
            accepted ? call.Acceptance.InvocationGrantId : null, accepted ? call.Acceptance : null, new ToolAlias("provider-tool"),
            accepted ? call.ToolId : null, accepted ? call.ToolVersion : null, accepted ? call.Effects : null,
            accepted ? call.ExternalIdempotencyKey : null, call.Admission, status, content ?? [],
            error ?? new ToolError(ToolErrorKind.Tool, "The tool failed.", "E_FAIL", TimeSpan.FromSeconds(3), ExtensionData.Empty),
            SideEffectCertainty.Unknown, usage, retryable: false, normalization,
            new ToolResultNormalizationInfo([ToolResultNormalizationTransformation.Truncated], 100, 3, 40, 1, ExtensionData.Empty),
            normalization.ProjectionPolicy, RecordedAt, accepted ? RecordedAt.AddSeconds(1) : null, RecordedAt.AddSeconds(2),
            extensions ?? ExtensionData.Empty);
    }

    internal static ToolCallTerminalSessionEntry TerminalEntry(ToolCallResult? result = null) =>
        new(new SessionEntryId(Id(72)), Address, Correlation, BranchId, new SessionSequence(13), new SessionEntryId(Id(70)),
            RecordedAt, new SchemaVersion("1"), result ?? Terminal());

    internal static bool Equivalent(ToolCallAcceptedSessionEntry expected, ToolCallAcceptedSessionEntry actual) =>
        EnvelopeEquivalent(expected, actual) && expected.Call == actual.Call;

    internal static bool Equivalent(ToolCallTerminalSessionEntry expected, ToolCallTerminalSessionEntry actual) =>
        EnvelopeEquivalent(expected, actual) && expected.Result == actual.Result;

    private static bool EnvelopeEquivalent(SessionEntry expected, SessionEntry actual) =>
        expected.Id == actual.Id && expected.Address == actual.Address && expected.Correlation == actual.Correlation
        && expected.BranchId == actual.BranchId && expected.Sequence == actual.Sequence
        && expected.CausalParentId == actual.CausalParentId && expected.RecordedAt == actual.RecordedAt
        && expected.SchemaVersion == actual.SchemaVersion;

    private static ToolResultNormalizationSnapshot Normalization(ToolExecutionPolicyReference? execution) => new(
        new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(2)),
        new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(3)),
        execution, new ToolResultNormalizationAlgorithmVersion(4), new ToolResultBounds(2048, 8),
        ToolResultProjectionTransformations.Truncation | ToolResultProjectionTransformations.Redaction, ExtensionData.Empty);

    private static SecurityAuthorizationContext Authorization()
    {
        var claim = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "operator", IdentityClaimValueKind.Text);
        var authenticatedAt = new DateTimeOffset(1970, 1, 1, 2, 0, 0, TimeSpan.FromHours(2));
        var evidence = new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"),
            "mfa", authenticatedAt, authenticatedAt.AddHours(1),
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:evidence")));
        var link = new DelegationIdentityLink(new DelegationId(Id(50)), new TenantId("tenant"), new PrincipalId("parent"),
            new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("parent-evidence"), new IdentityVersion(1),
            authenticatedAt, [claim], IdentityAssuranceLevel.Basic);
        var identity = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human,
            evidence, [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        return new SecurityAuthorizationContext(new SecurityProfileKey("secure"), new SecurityProfileVersion(2),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Id(40)), new SecurityPolicyVersion(5), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("primary"), new AgentDefinitionRevision(6), new ConfigurationVersion(3),
            new SecurityAuthorizationScope(AgentId, SessionId, Correlation), identity);
    }

    private static AgentId AgentId { get; } = new(Id(2));
    private static SessionId SessionId { get; } = new(Id(3));
    private static OperationId OperationId { get; } = new(Id(4));
    private static RunId RunId { get; } = new(Id(7));
    private static TurnId TurnId { get; } = new(Id(8));
    private static ToolCallId CallId { get; } = new(Id(9));
    private static BranchId BranchId { get; } = new(Id(5));
    private static SessionAddress Address { get; } = new(AgentId, SessionId);
    private static InRunOperationCorrelation Correlation { get; } = new(OperationId, RunId, TurnId);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
