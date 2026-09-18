// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies every static factory, guard, and the custom structural equality of one journaled transition record.</summary>
/// <remarks>
/// <see cref="JsonBudgetLedgerRecord"/> overrides <see cref="object.Equals(object?)"/> and <see cref="object.GetHashCode"/>
/// to compare its ordered batch-entry sequence element-wise rather than by backing-array identity. These cases exercise
/// every factory's guard, the custom comparison's branches, and an actual JSON round trip using the same fully populated
/// shape <see cref="JsonBudgetLedgerProbe"/> exercises at initialization.
/// </remarks>
public sealed class JsonBudgetLedgerRecordTests
{
    private static readonly BudgetLedgerScopeCreateRequest _scopeRequest = new(
        new BudgetScopeRequest(
            null,
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null),
            [],
            new IdempotencyKey("scope-key")),
        new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled));

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForScopeCreated"/> refuses a null request.</summary>
    [Fact]
    public void ForScopeCreated_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetLedgerRecord.ForScopeCreated(null!, new(Guid.NewGuid())))
            .ParamName.ShouldBe("request");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForScopeCreated"/> refuses a default scope identity.</summary>
    [Fact]
    public void ForScopeCreated_WhenScopeIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => JsonBudgetLedgerRecord.ForScopeCreated(_scopeRequest, default))
            .ParamName.ShouldBe("scopeId");

    /// <summary>Verifies a valid scope-creation record carries the full projected request and the allocated identity.</summary>
    [Fact]
    public void ForScopeCreated_WhenArgumentsAreValid_CarriesProjectedRequestAndScopeId()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());

        var record = JsonBudgetLedgerRecord.ForScopeCreated(_scopeRequest, scopeId);

        record.Kind.ShouldBe(JsonBudgetLedgerRecordKind.ScopeCreated);
        record.ScopeId.ShouldBe(scopeId.Value);
        record.Scope.ShouldBe(JsonBudgetScopeCreateRequest.FromDomain(_scopeRequest));
    }

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForBatchReserved"/> refuses a default receiving scope identity.</summary>
    [Fact]
    public void ForBatchReserved_WhenScopeIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForBatchReserved(default, [CreateReceipt()], DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("scopeId");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForBatchReserved"/> refuses an empty receipt sequence, because an atomic batch must carry at least one member.</summary>
    [Fact]
    public void ForBatchReserved_WhenReceiptsIsEmpty_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => JsonBudgetLedgerRecord.ForBatchReserved(
                new BudgetScopeId(Guid.NewGuid()), [], DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("receipts");

    /// <summary>Verifies a valid batch record carries every ordered entry and the captured clock instant.</summary>
    [Fact]
    public void ForBatchReserved_WhenArgumentsAreValid_CarriesOrderedEntriesAndOccurred()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var occurred = DateTimeOffset.UnixEpoch;

        var record = JsonBudgetLedgerRecord.ForBatchReserved(scopeId, [CreateReceipt()], occurred);

        record.Kind.ShouldBe(JsonBudgetLedgerRecordKind.BatchReserved);
        record.ScopeId.ShouldBe(scopeId.Value);
        record.Entries.Length.ShouldBe(1);
        record.Occurred.ShouldBe(occurred);
    }

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForStartMarked"/> refuses a default reservation identity.</summary>
    [Fact]
    public void ForStartMarked_WhenReservationIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForStartMarked(default, DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("reservationId");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForReleased"/> refuses a default reservation identity.</summary>
    [Fact]
    public void ForReleased_WhenReservationIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => JsonBudgetLedgerRecord.ForReleased(default))
            .ParamName.ShouldBe("reservationId");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForSettled"/> refuses a default reservation identity.</summary>
    [Fact]
    public void ForSettled_WhenReservationIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => JsonBudgetLedgerRecord.ForSettled(default, 1))
            .ParamName.ShouldBe("reservationId");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForSettled"/> refuses negative actual usage.</summary>
    [Fact]
    public void ForSettled_WhenActualIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForSettled(new(Guid.NewGuid()), -1))
            .ParamName.ShouldBe("actual");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForCorrected"/> refuses a non-positive revision.</summary>
    [Fact]
    public void ForCorrected_WhenRevisionIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForCorrected(new(Guid.NewGuid()), 1, 0, DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("revision");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForCorrected"/> refuses negative corrected usage.</summary>
    [Fact]
    public void ForCorrected_WhenCorrectedActualIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForCorrected(new(Guid.NewGuid()), -1, 1, DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("correctedActual");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForReconciled"/> refuses null evidence.</summary>
    [Fact]
    public void ForReconciled_WhenEvidenceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(
                () => JsonBudgetLedgerRecord.ForReconciled(new(Guid.NewGuid()), null!, new("key")))
            .ParamName.ShouldBe("evidence");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForReconciled"/> refuses a default reservation identity.</summary>
    [Fact]
    public void ForReconciled_WhenReservationIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => JsonBudgetLedgerRecord.ForReconciled(default, new BudgetStillUnknown(), new("key")))
            .ParamName.ShouldBe("reservationId");

    /// <summary>
    /// Verifies <see cref="JsonBudgetLedgerRecord.ForReconciled"/> refuses a blank replay key. A default
    /// <see cref="IdempotencyKey"/> is used because the validating constructor already refuses blank text before this
    /// factory's own guard could run.
    /// </summary>
    [Fact]
    public void ForReconciled_WhenIdempotencyKeyIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(
                () => JsonBudgetLedgerRecord.ForReconciled(new(Guid.NewGuid()), new BudgetStillUnknown(), default))
            .ParamName.ShouldBe("idempotencyKey");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForOverrunHoldResolution"/> refuses a null request.</summary>
    [Fact]
    public void ForOverrunHoldResolution_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(
                () => JsonBudgetLedgerRecord.ForOverrunHoldResolution(null!, DateTimeOffset.UnixEpoch))
            .ParamName.ShouldBe("request");

    /// <summary>Verifies <see cref="JsonBudgetLedgerRecord.ForExpirySweep"/> carries only the captured clock instant.</summary>
    [Fact]
    public void ForExpirySweep_WhenCalled_CarriesOnlyOccurred()
    {
        var occurred = DateTimeOffset.UnixEpoch;

        var record = JsonBudgetLedgerRecord.ForExpirySweep(occurred);

        record.Kind.ShouldBe(JsonBudgetLedgerRecordKind.ExpirySwept);
        record.Occurred.ShouldBe(occurred);
        record.ScopeId.ShouldBeNull();
        record.ReservationId.ShouldBeNull();
    }

    /// <summary>Verifies a record whose <see cref="JsonBudgetLedgerRecord.Reservations"/> member is a default array normalizes to an empty <see cref="JsonBudgetLedgerRecord.Entries"/> sequence.</summary>
    [Fact]
    public void Entries_WhenReservationsIsDefaultArray_NormalizesToEmpty()
    {
        var record = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.ExpirySwept, null, null, default, null, null, null, null, null, null, null, null);

        record.Entries.ShouldBeEmpty();
    }

    /// <summary>Verifies two records with identical scalar members and batch entries in the same order compare equal.</summary>
    [Fact]
    public void Equals_WhenEveryFieldAndEntryOrderMatch_ReportsEquality()
    {
        var first = JsonBudgetLedgerProbe.Create();
        var second = JsonBudgetLedgerProbe.Create();

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    /// <summary>Verifies records with a different discriminator are not equal.</summary>
    [Fact]
    public void Equals_WhenKindDiffers_ReportsInequality()
    {
        var first = JsonBudgetLedgerRecord.ForExpirySweep(DateTimeOffset.UnixEpoch);
        var second = first with { Kind = JsonBudgetLedgerRecordKind.Released, ReservationId = Guid.NewGuid() };

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies records whose batch-entry sequences differ only in length are not equal.</summary>
    [Fact]
    public void Equals_WhenEntrySequenceLengthsDiffer_ReportsInequality()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var first = JsonBudgetLedgerRecord.ForBatchReserved(scopeId, [CreateReceipt()], DateTimeOffset.UnixEpoch);
        var second = JsonBudgetLedgerRecord.ForBatchReserved(
            scopeId, [CreateReceipt(), CreateReceipt("second")], DateTimeOffset.UnixEpoch);

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies records whose batch-entry sequences contain the same members in a different order are not equal.</summary>
    [Fact]
    public void Equals_WhenEntryOrderDiffers_ReportsInequality()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var a = CreateReceipt("a");
        var b = CreateReceipt("b");
        var first = JsonBudgetLedgerRecord.ForBatchReserved(scopeId, [a, b], DateTimeOffset.UnixEpoch);
        var second = JsonBudgetLedgerRecord.ForBatchReserved(scopeId, [b, a], DateTimeOffset.UnixEpoch);

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies a record with a default (never-assigned) entry array compares equal to one with an explicit empty array.</summary>
    [Fact]
    public void Equals_WhenOneEntryArrayIsDefaultAndOtherIsEmpty_ReportsEquality()
    {
        var withDefault = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.ExpirySwept, null, null, default, null, null, null, null, null, null, null, null);
        var withEmpty = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.ExpirySwept, null, null, [], null, null, null, null, null, null, null, null);

        withEmpty.ShouldBe(withDefault);
        withEmpty.GetHashCode().ShouldBe(withDefault.GetHashCode());
    }

    /// <summary>Verifies comparison against null reports inequality without throwing.</summary>
    [Fact]
    public void Equals_WhenOtherIsNull_ReportsInequality() =>
        JsonBudgetLedgerRecord.ForExpirySweep(DateTimeOffset.UnixEpoch).Equals(null).ShouldBeFalse();

    /// <summary>Verifies the fully populated probe record survives an actual JSON encode and decode round trip byte for byte.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = JsonBudgetLedgerProbe.Create();
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetLedgerRecord>(
            JsonStoreSerialization.Encode(original, options, 8_192), options);

        decoded.ShouldBe(original);
    }

    private static BudgetLedgerReservationReceipt CreateReceipt(string key = "item")
    {
        var scope = new BudgetLedgerScopeReference(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null));
        var request = new BudgetReservationRequest(
            scope.Id, new BudgetDimension("tokens"), 1, new BudgetUnit("count"), new OperationId(Guid.NewGuid()), null, new IdempotencyKey(key));
        return new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())),
            request,
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
    }
}
