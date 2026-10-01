// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>Builds deterministic run events for exporter tests.</summary>
internal static class RunEventTestData
{
    private static readonly AgentId _agentId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId _sessionId = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly RunId _runId = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static readonly TurnId _turnId = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));

    /// <summary>Creates a content-delta event carrying <paramref name="delta"/>.</summary>
    /// <param name="delta">The content fragment.</param>
    /// <param name="sequence">The per-run sequence.</param>
    /// <returns>The immutable event.</returns>
    public static ContentDeltaEvent Content(ContentDelta delta, long sequence = 1) =>
        new(_agentId, _sessionId, null, _runId, _turnId, sequence, DateTimeOffset.UnixEpoch, new ModelRequestId(Guid.Parse("50000000-0000-0000-0000-000000000005")), 0, delta);

    /// <summary>Creates a content-delta event carrying visible text.</summary>
    /// <param name="text">The text fragment.</param>
    /// <returns>The immutable event.</returns>
    public static ContentDeltaEvent Text(string text) => Content(new TextContentDelta(text));

    /// <summary>Creates a content-free committed-message event.</summary>
    /// <param name="sequence">The per-run sequence.</param>
    /// <returns>The immutable event.</returns>
    public static MessageCommittedEvent Committed(long sequence = 2) =>
        new(_agentId, _sessionId, null, _runId, _turnId, sequence, DateTimeOffset.UnixEpoch, new MessageId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new SessionVersion(1));

    /// <summary>Creates a security audit record for exporter tests.</summary>
    /// <returns>The immutable pre-redacted record.</returns>
    public static SecurityAuditRecord Audit() =>
        new(
            new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")),
            new SecurityAuthorizationScope(
                _agentId,
                _sessionId,
                new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("a5555555-5555-5555-5555-555555555555")))),
            new SecurityRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666")),
            grantId: null,
            approvalRequestId: null,
            SecurityAuditEventKind.Decision,
            SecurityAuditOutcome.Accepted,
            new SecurityPolicyVersion(1),
            [],
            DateTimeOffset.UnixEpoch);
}
