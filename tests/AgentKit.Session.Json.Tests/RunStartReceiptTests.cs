// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the replay evidence retained for one successful atomic run start.</summary>
/// <remarks>
/// <see cref="RunStartReceipt"/> pairs an accepted run-start request with its committed result, which requires a fully
/// populated <see cref="SessionAcceptedRunState"/>. Rather than hand-building that large shape, this fixture drives one
/// real acceptance through <see cref="JsonSessionStoreConformanceFixture"/> and reuses the exact request and result the
/// store itself produced.
/// </remarks>
public sealed class RunStartReceiptTests
{
    /// <summary>Verifies the receipt retains the exact accepted request and result and supports value equality.</summary>
    [Fact]
    public async Task Constructor_WhenBuiltFromAnAcceptedRun_RetainsRequestAndResultAndSupportsEquality()
    {
        await using var fixture = new JsonSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (start, accepted) = await AcceptOneRunAsync(fixture, store);

        var receipt = new RunStartReceipt(start, accepted);
        var clone = new RunStartReceipt(start, accepted);

        receipt.Request.ShouldBeSameAs(start);
        receipt.Result.ShouldBeSameAs(accepted);
        receipt.ShouldBe(clone);
        receipt.ToString().ShouldContain(nameof(RunStartReceipt.Request));
    }

    private static async ValueTask<(SessionRunStartRequest Start, SessionRunAccepted Accepted)> AcceptOneRunAsync(
        JsonSessionStoreConformanceFixture fixture, ISessionStore store)
    {
        var create = JsonSessionStoreTests.CreateStoreRequest();
        var descriptor = (await store.CreateAsync(
            await fixture.AuthorizeAsync(create, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>().Descriptor;
        var laneId = JsonSessionStoreTests.Identifier<ExecutionLaneId>(320);
        var laneContext = JsonSessionStoreTests.Context(descriptor.Address, laneId,
            new BeforeRunOperationCorrelation(JsonSessionStoreTests.Identifier<OperationId>(321), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            JsonSessionStoreTests.Identifier<SessionEntryId>(322), JsonSessionStoreTests.Profile(),
            JsonSessionStoreTests.Configuration(), JsonSessionStoreTests.Timestamp(320),
            new IdempotencyKey("receipt-provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var admission = JsonSessionStoreTests.AdmissionRequest(
            laneContext, JsonSessionStoreTests.Identifier<AdmissionId>(323),
            JsonSessionStoreTests.Identifier<InputId>(324), JsonSessionStoreTests.Identifier<SessionEntryId>(325),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "receipt-admit");
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var runId = JsonSessionStoreTests.Identifier<RunId>(330);
        var turnId = JsonSessionStoreTests.Identifier<TurnId>(331);
        var start = new SessionRunStartRequest(
            laneContext, accepted.Receipt.AdmissionId, [accepted.Receipt.AdmissionId], accepted.Receipt.AdmittedSequence,
            new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionVersion(provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId,
            JsonSessionStoreTests.Identifier<SessionEntryId>(332), [JsonSessionStoreTests.Identifier<SessionEntryId>(333)],
            [JsonSessionStoreTests.Identifier<MessageId>(334)], JsonSessionStoreTests.Identifier<SessionEntryId>(335),
            new OperationStateRevision(1), JsonSessionStoreTests.Profile(), JsonSessionStoreTests.Configuration(),
            AuthorizationFor(descriptor.Address.AgentId, descriptor.Address.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId), laneContext.Identity),
            JsonSessionStoreTests.Timestamp(330), new IdempotencyKey("receipt-accept"));
        var result = (SessionRunAccepted) await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        return (start, result);
    }

    private static SecurityAuthorizationContext AuthorizationFor(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
        new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(JsonSessionStoreTests.Identifier<SecurityPolicySnapshotId>(336),
                new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
            new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
}
