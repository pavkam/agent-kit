// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist ledger evidence.</summary>
/// <remarks>
/// Because a host may replace the serializer contract entirely, initialization encodes and decodes one synthetic record
/// that simultaneously populates every member the journal ever writes: complete scope admission evidence with limits, an
/// ordered batch entry with its allocated identity and resolved expiry, exact decimal quantities, a nullable instant, a
/// correction revision, discriminated reconciliation evidence, an overrun generation, and a full consumed-intent receipt
/// with a delegated identity. Failing this check at bootstrap is far safer than discovering an unusable contract while
/// committing accounting that a caller already depends on.
/// </remarks>
internal static class JsonBudgetLedgerProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A record whose every member is populated with fixed, contract-exercising evidence.</returns>
    /// <remarks>The value uses fixed identities and instants so the check never depends on a clock, a random source, or the host locale.</remarks>
    internal static JsonBudgetLedgerRecord Create()
    {
        var occurred = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresAt = occurred.AddMinutes(5);
        var address = new BudgetScopeAddress(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            new AgentId(new Guid("11111111-1111-1111-1111-111111111111")),
            new SessionId(new Guid("22222222-2222-2222-2222-222222222222")),
            new RunId(new Guid("33333333-3333-3333-3333-333333333333")),
            null);
        var boundary = new BudgetLedgerScopeReference(
            new BudgetScopeId(new Guid("44444444-4444-4444-4444-444444444444")), address);
        var reservation = new BudgetLedgerReservationReference(
            boundary, new BudgetReservationId(new Guid("55555555-5555-5555-5555-555555555555")));
        var scopeRequest = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(
                new BudgetScopeId(new Guid("66666666-6666-6666-6666-666666666666")),
                address,
                [new BudgetLimit(new BudgetDimension("probe.dimension"), 1024.5m, new BudgetUnit("count"), BudgetLimitKind.Hard)],
                new IdempotencyKey("probe-scope-key")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution));
        var receipt = new BudgetLedgerReservationReceipt(
            reservation,
            new BudgetReservationRequest(
                boundary.Id,
                new BudgetDimension("probe.dimension"),
                7.25m,
                new BudgetUnit("count"),
                new OperationId(new Guid("77777777-7777-7777-7777-777777777777")),
                expiresAt,
                new IdempotencyKey("probe-item-key")),
            new BudgetEffectiveReservation(expiresAt));
        var hold = new BudgetOverrunHoldReference(boundary, reservation, new BudgetAccountingRevision(2));
        return new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.BatchReserved,
            boundary.Id.Value,
            JsonBudgetScopeCreateRequest.FromDomain(scopeRequest),
            [JsonBudgetReservationEntry.FromDomain(receipt)],
            reservation.Id.Value,
            occurred,
            9.75m,
            3,
            JsonBudgetReconciliationEvidence.FromDomain(new BudgetActualMeasured(9.75m)),
            JsonBudgetOverrunHoldReference.FromDomain(hold),
            JsonSecurityEnforcementIntentReceipt.FromDomain(CreateReceipt(hold, occurred)),
            "probe-replay-key");
    }

    private static SecurityEnforcementIntentReceipt CreateReceipt(BudgetOverrunHoldReference hold, DateTimeOffset occurred)
    {
        Debug.Assert(hold is not null, "The probe constructs its hold generation before binding audit evidence.");
        var securityScope = new SecurityAuthorizationScope(
            hold.Boundary.Address.AgentId,
            hold.Boundary.Address.SessionId,
            new InRunOperationCorrelation(
                new OperationId(new Guid("88888888-8888-8888-8888-888888888888")),
                new RunId(new Guid("33333333-3333-3333-3333-333333333333")),
                new TurnId(new Guid("99999999-9999-9999-9999-999999999999"))));
        var identity = new ExecutionIdentity(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("probe-evidence"),
                new IdentityIssuerId("probe-issuer"),
                "probe-method",
                occurred,
                occurred.AddHours(1),
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            [new IdentityClaim(new IdentityIssuerId("probe-issuer"), "probe-type", "probe-value", IdentityClaimValueKind.Text)],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
        var enforcement = new SecurityEnforcementRequest(
            securityScope,
            identity,
            new ComponentId("budget-operator"),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            [BudgetOverrunSecurityBinding.Resource(hold)],
            BudgetOverrunSecurityBinding.Fingerprint(hold),
            new SecurityRevocationVersion(1));
        return new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new GrantId(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new SecurityRequestId(new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            enforcement,
            new FencingToken(1),
            new ContentHash("sha256:probe-effect"),
            occurred);
    }
}
