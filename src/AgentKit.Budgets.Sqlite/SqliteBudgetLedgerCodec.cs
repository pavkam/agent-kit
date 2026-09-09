// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Encodes closed immutable ledger evidence through a bounded version-one binary schema.</summary>
internal static class SqliteBudgetLedgerCodec
{
    private const uint _magic = 0x4C424B41;
    private const byte _version = 1;
    private static readonly Encoding _strictUtf8 = new UTF8Encoding(false, true);

    /// <summary>Encodes one closed supported evidence value through the strict version-one schema.</summary>
    /// <typeparam name="T">The compile-time immutable evidence type; its runtime shape must be a supported closed case.</typeparam>
    /// <param name="value">The non-null validated evidence.</param><param name="settings">The immutable collection bounds.</param>
    /// <param name="maximumBytes">The positive encoded envelope bound enforced before each growth.</param>
    /// <returns>A new exact versioned envelope no larger than <paramref name="maximumBytes"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive or the encoded evidence exceeds it.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is unsupported or a configured collection bound is exceeded.</exception>
    internal static byte[] Encode<T>(T value, SqliteBudgetLedgerSettings settings, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        using var stream = new BoundedWriteStream(maximumBytes);
        using var writer = new BinaryWriter(stream, _strictUtf8, leaveOpen: true);
        writer.Write(_magic);
        writer.Write(_version);
        switch (value)
        {
            case BudgetLedgerScopeCreateRequest request:
                writer.Write((byte) BudgetEvidenceKind.ScopeCreateRequest);
                WriteScopeCreateRequest(writer, request, settings);
                break;
            case BudgetLedgerBatchReserveRequest request:
                writer.Write((byte) BudgetEvidenceKind.BatchReserveRequest);
                WriteBatchReserveRequest(writer, request, settings);
                break;
            case BudgetLedgerBatchReserved result:
                writer.Write((byte) BudgetEvidenceKind.BatchReserved);
                WriteBatchReserved(writer, result, settings);
                break;
            case BudgetLedgerReservationReceipt receipt:
                writer.Write((byte) BudgetEvidenceKind.ReservationReceipt);
                WriteReceipt(writer, receipt);
                break;
            case BudgetStartExpired expired:
                writer.Write((byte) BudgetEvidenceKind.StartExpired);
                WriteGuid(writer, expired.ReservationId.Value);
                WriteInstant(writer, expired.EffectiveExpiry);
                break;
            case BudgetCommitResult commit:
                writer.Write((byte) BudgetEvidenceKind.CommitResult);
                WriteCommit(writer, commit, settings);
                break;
            case BudgetOverrunHold hold:
                writer.Write((byte) BudgetEvidenceKind.OverrunHold);
                WriteHold(writer, hold);
                break;
            case BudgetCorrectionResult correction:
                writer.Write((byte) BudgetEvidenceKind.CorrectionResult);
                WriteCorrection(writer, correction, settings);
                break;
            case BudgetReconciliationEvidence evidence:
                writer.Write((byte) BudgetEvidenceKind.ReconciliationEvidence);
                WriteReconciliationEvidence(writer, evidence);
                break;
            case BudgetLedgerReconciliationResult reconciliation:
                writer.Write((byte) BudgetEvidenceKind.ReconciliationResult);
                WriteReconciliationResult(writer, reconciliation, settings);
                break;
            case BudgetOverrunHoldResolutionRequest resolutionRequest:
                writer.Write((byte) BudgetEvidenceKind.OverrunResolutionRequest);
                WriteResolutionRequest(writer, resolutionRequest, settings);
                break;
            case BudgetOverrunHoldResolutionResult resolutionResult:
                writer.Write((byte) BudgetEvidenceKind.OverrunResolutionResult);
                WriteResolutionResult(writer, resolutionResult, settings);
                break;
            default:
                throw new ArgumentException("The budget evidence type is not supported by schema version one.", nameof(value));
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes one exact supported value while rejecting unknown versions, kinds, trailing bytes, and excessive collections.</summary>
    /// <typeparam name="T">The exact expected closed evidence type.</typeparam>
    /// <param name="payload">The nonempty bounded persisted envelope.</param><param name="settings">The immutable decode bounds.</param>
    /// <param name="maximumBytes">The positive maximum accepted envelope length.</param>
    /// <returns>The reconstructed validated immutable domain value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="InvalidDataException">The payload is empty, excessive, corrupt, unsupported, incomplete, invalid, or has trailing evidence.</exception>
    internal static T Decode<T>(byte[] payload, SqliteBudgetLedgerSettings settings, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (payload.Length is 0 || payload.Length > maximumBytes)
        {
            throw new InvalidDataException("Persisted budget evidence has an invalid encoded length.");
        }
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, _strictUtf8, leaveOpen: true);
            if (reader.ReadUInt32() != _magic || reader.ReadByte() != _version)
            {
                throw new InvalidDataException("Persisted budget evidence has an unsupported envelope.");
            }
            var kind = (BudgetEvidenceKind) reader.ReadByte();
            object result = typeof(T) switch
            {
                var type when type == typeof(BudgetLedgerScopeCreateRequest) && kind == BudgetEvidenceKind.ScopeCreateRequest => ReadScopeCreateRequest(reader, settings),
                var type when type == typeof(BudgetLedgerBatchReserveRequest) && kind == BudgetEvidenceKind.BatchReserveRequest => ReadBatchReserveRequest(reader, settings),
                var type when type == typeof(BudgetLedgerBatchReserved) && kind == BudgetEvidenceKind.BatchReserved => ReadBatchReserved(reader, settings),
                var type when type == typeof(BudgetLedgerReservationReceipt) && kind == BudgetEvidenceKind.ReservationReceipt => ReadReceipt(reader),
                var type when type == typeof(BudgetStartExpired) && kind == BudgetEvidenceKind.StartExpired => new BudgetStartExpired(new BudgetReservationId(ReadGuid(reader)), ReadInstant(reader)),
                var type when type == typeof(BudgetCommitResult) && kind == BudgetEvidenceKind.CommitResult => ReadCommit(reader, settings),
                var type when type == typeof(BudgetOverrunHold) && kind == BudgetEvidenceKind.OverrunHold => ReadHold(reader),
                var type when type == typeof(BudgetCorrectionResult) && kind == BudgetEvidenceKind.CorrectionResult => ReadCorrection(reader, settings),
                var type when type == typeof(BudgetReconciliationEvidence) && kind == BudgetEvidenceKind.ReconciliationEvidence => ReadReconciliationEvidence(reader),
                var type when type == typeof(BudgetLedgerReconciliationResult) && kind == BudgetEvidenceKind.ReconciliationResult => ReadReconciliationResult(reader, settings),
                var type when type == typeof(BudgetOverrunHoldResolutionRequest) && kind == BudgetEvidenceKind.OverrunResolutionRequest => ReadResolutionRequest(reader, settings),
                var type when type == typeof(BudgetOverrunHoldResolutionResult) && kind == BudgetEvidenceKind.OverrunResolutionResult => ReadResolutionResult(reader, settings),
                _ => throw new InvalidDataException("Persisted budget evidence has an unexpected payload kind."),
            };
            return stream.Position != stream.Length
                ? throw new InvalidDataException("Persisted budget evidence contains trailing bytes.")
                : (T) result;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or DecoderFallbackException or EndOfStreamException or OverflowException)
        {
            throw new InvalidDataException("Persisted budget evidence is malformed.", exception);
        }
    }

    private static void WriteScopeCreateRequest(BinaryWriter writer, BudgetLedgerScopeCreateRequest request, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && request is not null, "Validated codec inputs are required.");
        WriteOptionalGuid(writer, request.OriginalRequest.ParentScopeId?.Value);
        WriteAddress(writer, request.OriginalRequest.Address);
        WriteCount(writer, request.OriginalRequest.Limits.Length, settings.MaximumLimitsPerScope);
        foreach (var limit in request.OriginalRequest.Limits)
        {
            WriteString(writer, limit.Dimension.Value);
            writer.Write(limit.Value);
            WriteString(writer, limit.Unit.Value);
            writer.Write((byte) limit.Kind);
        }
        WriteString(writer, request.OriginalRequest.IdempotencyKey.Value);
        writer.Write(request.Admission.MaximumScopeDepth);
        writer.Write(request.Admission.MaximumOpenReservationsPerScope);
        writer.Write(request.Admission.DefaultReservationLifetime.Ticks);
        writer.Write((byte) request.Admission.OverrunHoldPolicy);
    }

    private static BudgetLedgerScopeCreateRequest ReadScopeCreateRequest(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        BudgetScopeId? parent = ReadOptionalGuid(reader) is { } parentValue ? new BudgetScopeId(parentValue) : null;
        var address = ReadAddress(reader);
        var count = ReadCount(reader, settings.MaximumLimitsPerScope);
        var limits = ImmutableArray.CreateBuilder<BudgetLimit>(count);
        for (var index = 0; index < count; index++)
        {
            limits.Add(new(new(ReadString(reader)), reader.ReadDecimal(), new(ReadString(reader)), (BudgetLimitKind) reader.ReadByte()));
        }
        var original = new BudgetScopeRequest(parent, address, limits.MoveToImmutable(), new(ReadString(reader)));
        var admission = new BudgetScopeAdmission(reader.ReadInt32(), reader.ReadInt32(), TimeSpan.FromTicks(reader.ReadInt64()), (BudgetOverrunHoldPolicy) reader.ReadByte());
        return new(original, admission);
    }

    private static void WriteBatchReserveRequest(BinaryWriter writer, BudgetLedgerBatchReserveRequest request, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && request is not null, "Validated codec inputs are required.");
        WriteScopeReference(writer, request.Scope);
        WriteCount(writer, request.OriginalRequests.Length, settings.MaximumBatchSize);
        foreach (var item in request.OriginalRequests)
        {
            WriteReservationRequest(writer, item);
        }
    }

    private static BudgetLedgerBatchReserveRequest ReadBatchReserveRequest(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var scope = ReadScopeReference(reader);
        var count = ReadCount(reader, settings.MaximumBatchSize);
        var requests = ImmutableArray.CreateBuilder<BudgetReservationRequest>(count);
        for (var index = 0; index < count; index++)
        {
            requests.Add(ReadReservationRequest(reader));
        }
        return new(scope, requests.MoveToImmutable());
    }

    private static void WriteBatchReserved(BinaryWriter writer, BudgetLedgerBatchReserved result, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && result is not null, "Validated codec inputs are required.");
        WriteCount(writer, result.Receipts.Length, settings.MaximumBatchSize);
        foreach (var receipt in result.Receipts)
        {
            WriteReceipt(writer, receipt);
        }
    }

    private static BudgetLedgerBatchReserved ReadBatchReserved(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var count = ReadCount(reader, settings.MaximumBatchSize);
        var receipts = ImmutableArray.CreateBuilder<BudgetLedgerReservationReceipt>(count);
        for (var index = 0; index < count; index++)
        {
            receipts.Add(ReadReceipt(reader));
        }
        return new(receipts.MoveToImmutable());
    }

    private static void WriteReceipt(BinaryWriter writer, BudgetLedgerReservationReceipt receipt)
    {
        Debug.Assert(writer is not null && receipt is not null, "Validated codec inputs are required.");
        WriteReservationReference(writer, receipt.Reservation);
        WriteReservationRequest(writer, receipt.OriginalRequest);
        WriteInstant(writer, receipt.EffectiveReservation.ExpiresAt);
    }

    private static BudgetLedgerReservationReceipt ReadReceipt(BinaryReader reader) => new(ReadReservationReference(reader), ReadReservationRequest(reader), new(ReadInstant(reader)));

    private static void WriteCommit(BinaryWriter writer, BudgetCommitResult commit, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && commit is not null, "Validated codec inputs are required.");
        WriteGuid(writer, commit.ReservationId.Value);
        writer.Write(commit.Reserved);
        writer.Write(commit.Actual);
        writer.Write(commit.Released);
        writer.Write(commit.Overrun);
        writer.Write(commit.AccountingRevision.HasValue);
        if (commit.AccountingRevision is { } revision)
        {
            writer.Write(revision.Value);
        }
        WriteCount(writer, commit.CreatedOverrunHolds.Length, settings.MaximumLineageDepth);
        foreach (var hold in commit.CreatedOverrunHolds)
        {
            WriteHold(writer, hold);
        }
    }

    private static BudgetCommitResult ReadCommit(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var id = new BudgetReservationId(ReadGuid(reader));
        var reserved = reader.ReadDecimal();
        var actual = reader.ReadDecimal();
        var released = reader.ReadDecimal();
        var overrun = reader.ReadDecimal();
        BudgetAccountingRevision? revision = reader.ReadBoolean() ? new(reader.ReadInt64()) : null;
        var count = ReadCount(reader, settings.MaximumLineageDepth);
        var holds = ImmutableArray.CreateBuilder<BudgetOverrunHold>(count);
        for (var index = 0; index < count; index++)
        {
            holds.Add(ReadHold(reader));
        }
        return new(id, reserved, actual, released, overrun, revision, holds.MoveToImmutable());
    }

    private static void WriteHold(BinaryWriter writer, BudgetOverrunHold hold)
    {
        Debug.Assert(writer is not null && hold is not null, "Validated codec inputs are required.");
        WriteScopeReference(writer, hold.Reference.Boundary);
        WriteReservationReference(writer, hold.Reference.Reservation);
        writer.Write(hold.Reference.TriggeringRevision.Value);
        WriteString(writer, hold.Dimension.Value);
        WriteString(writer, hold.Unit.Value);
        writer.Write(hold.Reserved);
        writer.Write(hold.CurrentActual);
        writer.Write((byte) hold.Policy);
    }

    private static BudgetOverrunHold ReadHold(BinaryReader reader) => new(
        new(ReadScopeReference(reader), ReadReservationReference(reader), new(reader.ReadInt64())),
        new(ReadString(reader)), new(ReadString(reader)), reader.ReadDecimal(), reader.ReadDecimal(),
        (BudgetOverrunHoldPolicy) reader.ReadByte());

    private static void WriteHoldReference(BinaryWriter writer, BudgetOverrunHoldReference reference)
    {
        Debug.Assert(writer is not null && reference is not null, "Validated codec inputs are required.");
        WriteScopeReference(writer, reference.Boundary);
        WriteReservationReference(writer, reference.Reservation);
        writer.Write(reference.TriggeringRevision.Value);
    }

    private static BudgetOverrunHoldReference ReadHoldReference(BinaryReader reader) => new(
        ReadScopeReference(reader), ReadReservationReference(reader), new(reader.ReadInt64()));

    private static void WriteCorrection(BinaryWriter writer, BudgetCorrectionResult result, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && result is not null, "Validated codec inputs are required.");
        WriteGuid(writer, result.ReservationId.Value);
        writer.Write(result.PreviousActual);
        writer.Write(result.CorrectedActual);
        writer.Write(result.Revision);
        writer.Write(result.AccountingRevision.HasValue);
        if (result.AccountingRevision is { } accountingRevision)
        {
            writer.Write(accountingRevision.Value);
        }
        WriteCount(writer, result.CreatedOverrunHolds.Length, settings.MaximumLineageDepth);
        foreach (var hold in result.CreatedOverrunHolds)
        {
            WriteHold(writer, hold);
        }
        WriteCount(writer, result.ClearedOverrunHolds.Length, settings.MaximumLineageDepth);
        foreach (var reference in result.ClearedOverrunHolds)
        {
            WriteHoldReference(writer, reference);
        }
    }

    private static BudgetCorrectionResult ReadCorrection(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var id = new BudgetReservationId(ReadGuid(reader));
        var previous = reader.ReadDecimal();
        var corrected = reader.ReadDecimal();
        var revision = reader.ReadInt64();
        BudgetAccountingRevision? accountingRevision = reader.ReadBoolean() ? new(reader.ReadInt64()) : null;
        var createdCount = ReadCount(reader, settings.MaximumLineageDepth);
        var created = ImmutableArray.CreateBuilder<BudgetOverrunHold>(createdCount);
        for (var index = 0; index < createdCount; index++)
        {
            created.Add(ReadHold(reader));
        }
        var clearedCount = ReadCount(reader, settings.MaximumLineageDepth);
        var cleared = ImmutableArray.CreateBuilder<BudgetOverrunHoldReference>(clearedCount);
        for (var index = 0; index < clearedCount; index++)
        {
            cleared.Add(ReadHoldReference(reader));
        }
        return new(id, previous, corrected, revision, accountingRevision, created.MoveToImmutable(), cleared.MoveToImmutable());
    }

    private static void WriteReconciliationEvidence(BinaryWriter writer, BudgetReconciliationEvidence evidence)
    {
        Debug.Assert(writer is not null && evidence is not null, "Validated codec inputs are required.");
        switch (evidence)
        {
            case BudgetActualMeasured measured:
                writer.Write((byte) 1);
                writer.Write(measured.Actual);
                break;
            case BudgetActualEstimated estimated:
                writer.Write((byte) 2);
                writer.Write(estimated.Actual);
                break;
            case BudgetNoUsageProven:
                writer.Write((byte) 3);
                break;
            case BudgetStillUnknown:
                writer.Write((byte) 4);
                break;
            default:
                throw new ArgumentException("The reconciliation evidence kind is unsupported.", nameof(evidence));
        }
    }

    private static BudgetReconciliationEvidence ReadReconciliationEvidence(BinaryReader reader) => reader.ReadByte() switch
    {
        1 => new BudgetActualMeasured(reader.ReadDecimal()),
        2 => new BudgetActualEstimated(reader.ReadDecimal()),
        3 => new BudgetNoUsageProven(),
        4 => new BudgetStillUnknown(),
        _ => throw new InvalidDataException("Persisted reconciliation evidence has an unknown kind."),
    };

    private static void WriteReconciliationResult(BinaryWriter writer, BudgetLedgerReconciliationResult result, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && result is not null, "Validated codec inputs are required.");
        switch (result)
        {
            case BudgetLedgerReconciliationSettled settled:
                writer.Write((byte) 1);
                WriteCommit(writer, settled.Commit, settings);
                break;
            case BudgetLedgerReconciliationReleased released:
                writer.Write((byte) 2);
                WriteReservationReference(writer, released.Reservation);
                break;
            case BudgetLedgerReconciliationRetainedUnknown unknown:
                writer.Write((byte) 3);
                WriteReservationReference(writer, unknown.Reservation);
                break;
            default:
                throw new ArgumentException("The reconciliation result kind is unsupported.", nameof(result));
        }
    }

    private static BudgetLedgerReconciliationResult ReadReconciliationResult(BinaryReader reader, SqliteBudgetLedgerSettings settings) => reader.ReadByte() switch
    {
        1 => new BudgetLedgerReconciliationSettled(ReadCommit(reader, settings)),
        2 => new BudgetLedgerReconciliationReleased(ReadReservationReference(reader)),
        3 => new BudgetLedgerReconciliationRetainedUnknown(ReadReservationReference(reader)),
        _ => throw new InvalidDataException("Persisted reconciliation result has an unknown kind."),
    };

    private static void WriteResolutionRequest(BinaryWriter writer, BudgetOverrunHoldResolutionRequest request, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && request is not null, "Validated codec inputs are required.");
        WriteHoldReference(writer, request.Hold);
        WriteEnforcementReceipt(writer, request.EnforcementReceipt, settings);
        WriteString(writer, request.IdempotencyKey.Value);
    }

    private static BudgetOverrunHoldResolutionRequest ReadResolutionRequest(BinaryReader reader, SqliteBudgetLedgerSettings settings) =>
        new(ReadHoldReference(reader), ReadEnforcementReceipt(reader, settings), new(ReadString(reader)));

    private static void WriteResolutionResult(BinaryWriter writer, BudgetOverrunHoldResolutionResult result, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null && result is not null, "Validated codec inputs are required.");
        switch (result)
        {
            case BudgetOverrunHoldResolved resolved:
                writer.Write((byte) 1);
                WriteHoldReference(writer, resolved.Hold);
                writer.Write(resolved.ResolutionRevision.Value);
                WriteEnforcementReceipt(writer, resolved.EnforcementReceipt, settings);
                break;
            case BudgetOverrunHoldResolutionBlocked blocked:
                writer.Write((byte) 2);
                WriteHoldReference(writer, blocked.Hold);
                WriteCount(writer, blocked.CurrentOverruns.Length, settings.MaximumLineageDepth);
                foreach (var hold in blocked.CurrentOverruns)
                {
                    WriteHold(writer, hold);
                }

                WriteCount(writer, blocked.HardLimitFailures.Length, settings.MaximumLimitsPerScope);
                foreach (var failure in blocked.HardLimitFailures)
                {
                    WriteLimitFailure(writer, failure);
                }

                break;
            default:
                throw new ArgumentException("The overrun-resolution result kind is unsupported.", nameof(result));
        }
    }

    private static BudgetOverrunHoldResolutionResult ReadResolutionResult(BinaryReader reader, SqliteBudgetLedgerSettings settings) => reader.ReadByte() switch
    {
        1 => new BudgetOverrunHoldResolved(ReadHoldReference(reader), new(reader.ReadInt64()), ReadEnforcementReceipt(reader, settings)),
        2 => ReadBlockedResolution(reader, settings),
        _ => throw new InvalidDataException("Persisted overrun-resolution result has an unknown kind."),
    };

    private static BudgetOverrunHoldResolutionBlocked ReadBlockedResolution(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var reference = ReadHoldReference(reader);
        var overruns = ImmutableArray.CreateBuilder<BudgetOverrunHold>(ReadCount(reader, settings.MaximumLineageDepth));
        while (overruns.Count < overruns.Capacity)
        {
            overruns.Add(ReadHold(reader));
        }

        var failures = ImmutableArray.CreateBuilder<BudgetLimitFailure>(ReadCount(reader, settings.MaximumLimitsPerScope));
        while (failures.Count < failures.Capacity)
        {
            failures.Add(ReadLimitFailure(reader));
        }

        return new(reference, overruns.MoveToImmutable(), failures.MoveToImmutable());
    }

    private static void WriteEnforcementReceipt(BinaryWriter writer, SecurityEnforcementIntentReceipt receipt, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(writer is not null && settings is not null, "Validated codec inputs are required.");
        WriteGuid(writer, receipt.IntentId.Value);
        WriteGuid(writer, receipt.GrantId.Value);
        WriteGuid(writer, receipt.RequestId.Value);
        var enforcement = SqliteBudgetSecurityCodec.EncodeEnforcement(receipt.Enforcement, settings);
        writer.Write(enforcement.Length);
        writer.Write(enforcement);
        writer.Write(receipt.RequiredFence.HasValue);
        if (receipt.RequiredFence is { } fence)
        {
            writer.Write(fence.Value);
        }

        WriteString(writer, receipt.EffectFingerprint.Value);
        WriteInstant(writer, receipt.ConsumedAt);
    }

    private static SecurityEnforcementIntentReceipt ReadEnforcementReceipt(BinaryReader reader, SqliteBudgetLedgerSettings settings)
    {
        Debug.Assert(reader is not null && settings is not null, "Validated codec inputs are required.");
        var intent = new SecurityEnforcementIntentId(ReadGuid(reader));
        var grant = new GrantId(ReadGuid(reader));
        var request = new SecurityRequestId(ReadGuid(reader));
        var length = ReadCount(reader, settings.MaximumPayloadBytes);
        var payload = reader.ReadBytes(length);
        if (payload.Length != length)
        {
            throw new EndOfStreamException();
        }

        var enforcement = SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings);
        FencingToken? fence = reader.ReadBoolean() ? new(reader.ReadInt64()) : null;
        return new(intent, grant, request, enforcement, fence, new(ReadString(reader)), ReadInstant(reader));
    }

    private static void WriteLimitFailure(BinaryWriter writer, BudgetLimitFailure failure)
    {
        Debug.Assert(writer is not null && failure is not null, "Validated codec inputs are required.");
        WriteGuid(writer, failure.ScopeId.Value);
        WriteString(writer, failure.Dimension.Value);
        writer.Write((byte) failure.Kind);
        writer.Write(failure.ConfiguredValue);
        WriteQuantity(writer, failure.ObservedValue);
        WriteQuantity(writer, failure.RequestedAmount);
        WriteString(writer, failure.Unit.Value);
        WriteString(writer, failure.SafeMessage);
    }

    private static BudgetLimitFailure ReadLimitFailure(BinaryReader reader) => new(new(ReadGuid(reader)), new(ReadString(reader)),
        (BudgetLimitKind) reader.ReadByte(), reader.ReadDecimal(), ReadQuantity(reader), ReadQuantity(reader), new(ReadString(reader)), ReadString(reader));

    private static void WriteQuantity(BinaryWriter writer, BudgetQuantity quantity)
    {
        Debug.Assert(writer is not null, "Validated codec inputs are required.");
        var byteCount = quantity.Coefficient.GetByteCount(isUnsigned: true);
        if (writer.BaseStream is BoundedWriteStream bounded)
        {
            bounded.EnsureCanWrite(checked(sizeof(int) + byteCount + sizeof(int)));
        }
        var bytes = new byte[byteCount];
        _ = quantity.Coefficient.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: true);
        writer.Write(byteCount);
        writer.Write(bytes);
        writer.Write(quantity.Scale);
    }

    private static BudgetQuantity ReadQuantity(BinaryReader reader)
    {
        Debug.Assert(reader is not null, "Validated codec inputs are required.");
        var length = reader.ReadInt32();
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position - sizeof(int);
        if (length < 0 || length > remaining)
        {
            throw new InvalidDataException("Persisted exact quantity has an invalid length.");
        }

        var bytes = reader.ReadBytes(length);
        return bytes.Length != length
            ? throw new EndOfStreamException()
            : new(new BigInteger(bytes, isUnsigned: true, isBigEndian: true), reader.ReadInt32());
    }

    private static void WriteReservationRequest(BinaryWriter writer, BudgetReservationRequest request)
    {
        Debug.Assert(writer is not null && request is not null, "Validated codec inputs are required.");
        WriteGuid(writer, request.ScopeId.Value);
        WriteString(writer, request.Dimension.Value);
        writer.Write(request.Amount);
        WriteString(writer, request.Unit.Value);
        WriteGuid(writer, request.OperationId.Value);
        writer.Write(request.ExpiresAt.HasValue);
        if (request.ExpiresAt is { } expiry)
        {
            WriteInstant(writer, expiry);
        }
        WriteString(writer, request.IdempotencyKey.Value);
    }

    private static BudgetReservationRequest ReadReservationRequest(BinaryReader reader) => new(
        new(ReadGuid(reader)), new(ReadString(reader)), reader.ReadDecimal(), new(ReadString(reader)), new(ReadGuid(reader)),
        reader.ReadBoolean() ? ReadInstant(reader) : null, new(ReadString(reader)));

    private static void WriteScopeReference(BinaryWriter writer, BudgetLedgerScopeReference reference)
    {
        Debug.Assert(writer is not null && reference is not null, "Validated codec inputs are required.");
        WriteGuid(writer, reference.Id.Value);
        WriteAddress(writer, reference.Address);
    }

    private static BudgetLedgerScopeReference ReadScopeReference(BinaryReader reader) => new(new(ReadGuid(reader)), ReadAddress(reader));

    private static void WriteReservationReference(BinaryWriter writer, BudgetLedgerReservationReference reference)
    {
        Debug.Assert(writer is not null && reference is not null, "Validated codec inputs are required.");
        WriteScopeReference(writer, reference.Scope);
        WriteGuid(writer, reference.Id.Value);
    }

    private static BudgetLedgerReservationReference ReadReservationReference(BinaryReader reader) => new(ReadScopeReference(reader), new(ReadGuid(reader)));

    private static void WriteAddress(BinaryWriter writer, BudgetScopeAddress address)
    {
        Debug.Assert(writer is not null && address is not null, "Validated codec inputs are required.");
        WriteString(writer, address.TenantId.Value);
        WriteString(writer, address.PrincipalId.Value);
        WriteGuid(writer, address.AgentId.Value);
        WriteOptionalGuid(writer, address.SessionId?.Value);
        WriteOptionalGuid(writer, address.RunId?.Value);
        WriteOptionalGuid(writer, address.OperationId?.Value);
    }

    private static BudgetScopeAddress ReadAddress(BinaryReader reader) => new(new(ReadString(reader)), new(ReadString(reader)), new(ReadGuid(reader)),
        ReadOptionalGuid(reader) is { } session ? new SessionId(session) : null,
        ReadOptionalGuid(reader) is { } run ? new RunId(run) : null,
        ReadOptionalGuid(reader) is { } operation ? new OperationId(operation) : null);

    private static void WriteInstant(BinaryWriter writer, DateTimeOffset value)
    {
        Debug.Assert(writer is not null, "Validated codec inputs are required.");
        writer.Write(value.Ticks);
        writer.Write(value.Offset.Ticks);
    }

    private static DateTimeOffset ReadInstant(BinaryReader reader) => new(reader.ReadInt64(), TimeSpan.FromTicks(reader.ReadInt64()));

    private static void WriteOptionalGuid(BinaryWriter writer, Guid? value)
    {
        Debug.Assert(writer is not null, "Validated codec inputs are required.");
        writer.Write(value.HasValue);
        if (value is { } present)
        {
            WriteGuid(writer, present);
        }
    }

    private static Guid? ReadOptionalGuid(BinaryReader reader) => reader.ReadBoolean() ? ReadGuid(reader) : null;

    private static void WriteGuid(BinaryWriter writer, Guid value) => writer.Write(value.ToByteArray());

    private static Guid ReadGuid(BinaryReader reader)
    {
        Debug.Assert(reader is not null, "Validated codec inputs are required.");
        var bytes = reader.ReadBytes(16);
        return bytes.Length == 16 ? new(bytes) : throw new EndOfStreamException();
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        Debug.Assert(writer is not null && value is not null, "Validated codec inputs are required.");
        var count = _strictUtf8.GetByteCount(value);
        if (writer.BaseStream is BoundedWriteStream bounded)
        {
            bounded.EnsureCanWrite(checked(sizeof(int) + count));
        }
        writer.Write(count);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(count, 1));
        try
        {
            var written = _strictUtf8.GetBytes(value, rented);
            writer.Write(rented, 0, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static string ReadString(BinaryReader reader)
    {
        Debug.Assert(reader is not null, "Validated codec inputs are required.");
        var length = reader.ReadInt32();
        if (length <= 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Persisted budget text has an invalid length.");
        }
        var bytes = reader.ReadBytes(length);
        return _strictUtf8.GetString(bytes);
    }

    private static void WriteCount(BinaryWriter writer, int count, int maximum)
    {
        Debug.Assert(writer is not null, "Validated codec inputs are required.");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, maximum);
        writer.Write(count);
    }

    private static int ReadCount(BinaryReader reader, int maximum)
    {
        Debug.Assert(reader is not null, "Validated codec inputs are required.");
        var count = reader.ReadInt32();
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        return count >= 0 && count <= maximum && count <= remaining
            ? count
            : throw new InvalidDataException("Persisted budget collection exceeds its configured bound.");
    }
}
