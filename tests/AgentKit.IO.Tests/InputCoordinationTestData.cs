// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

/// <summary>Supplies deterministic, consistently correlated admission and promotion evidence for coordinator fixtures.</summary>
internal static class InputCoordinationTestData
{
    internal static AgentId Agent { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    internal static SessionId Session { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    internal static ExecutionLaneId Lane { get; } = new(Guid.Parse("45000000-0000-0000-0000-000000000001"));
    internal static BranchId Branch { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    internal static TurnId Turn { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
    internal static TurnId NextTurn { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000002"));
    internal static SessionEntryId Entry { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000009"));

    internal static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    internal static InRunOperationCorrelation Operation() => new(
        new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
        new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
        Turn);

    internal static AgentInput Payload(string text = "input", int parts = 1, InputDelivery delivery = InputDelivery.Steer) =>
        new(new InputId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            delivery,
            [.. Enumerable.Range(0, parts).Select(index => new TextPart($"{text}-{index}", TextSemantics.Plain, ExtensionData.Empty))],
            ExtensionData.Empty);

    internal static SecurityAuthorizationContext Authorization(OperationCorrelation correlation) => new(
        new SecurityProfileKey("profile"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(1),
            new ContentHash("safe")),
        new ComponentKey<ISecurityAuthority>("authority"),
        new AgentDefinitionRevision(0),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(Agent, Session, correlation),
        Identity());

    internal static InputAdmissionRequest AdmissionRequest(AgentInput? payload = null)
    {
        var correlation = Operation();
        return new InputAdmissionRequest(
            Agent, Session, Lane, Identity(), correlation, Authorization(correlation), payload ?? Payload());
    }

    internal static InputPromotionRequest PromotionRequest(PromotionBoundary boundary = PromotionBoundary.AfterTurnCommitted)
    {
        var correlation = Operation();
        return new InputPromotionRequest(
            Agent, Session, Lane, correlation, new OperationStateRevision(2),
            new SessionBranchCursor(Branch, Entry), new SessionSequence(10), new SessionVersion(4), null,
            Identity(), Authorization(correlation), boundary, Turn, NextTurn, 16);
    }

    internal static AcceptedInput Accepted(AdmissionId admissionId, InputId inputId) => new(
        new AdmissionReceipt(admissionId, inputId, Agent, Session, Lane, new SessionSequence(7), existing: false));

    internal static InputPromotionRejected Rejected() => new(
        new InputRejection(InputRejectionKind.InvalidInput, "test rejection"));

    internal static InputPromoted Promoted()
    {
        var admissionId = new AdmissionId(Guid.Parse("15000000-0000-0000-0000-000000000001"));
        var payload = Payload();
        var manifest = new InputPreprocessingManifest(
            new ConfigurationVersion(1),
            InputPayloadFingerprint.Create(payload),
            InputPayloadFingerprint.Create(payload));
        var admitted = new AdmittedInput(
            admissionId, Agent, Session, Lane, Identity(), new SessionSequence(1), payload, payload, manifest, DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var snapshot = new InputPromotionSnapshot(
            Agent, Session, Lane, Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch, null),
            new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn, NextTurn, [admissionId]);
        return new InputPromoted(
            snapshot, [admitted], new SessionVersion(1), new SessionBranchCursor(Branch, null), new OperationStateRevision(2));
    }
}
