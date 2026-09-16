// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;



/// <summary>Verifies SqliteBudgetLedgerCodec behavior and contracts.</summary>
public sealed class SqliteBudgetLedgerCodecTests
{
    /// <summary>Proves unsupported, truncated, and trailing envelopes fail as persisted-data corruption.</summary>
    [Fact]
    public void Decode_WhenEnvelopeIsMalformed_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var payload = SqliteBudgetLedgerCodec.Encode(Request(), settings, settings.MaximumPayloadBytes);
        var unsupported = payload.ToArray();
        unsupported[4] = 99;
        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>(unsupported, settings, settings.MaximumPayloadBytes));
        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>(payload[..^1], settings, settings.MaximumPayloadBytes));
        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>([.. payload, 0], settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves the configured envelope bound rejects growth before producing an oversized payload.</summary>
    [Fact]
    public void Encode_WhenEnvelopeExceedsBound_ThrowsBeforeResultAllocation()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => SqliteBudgetLedgerCodec.Encode(Request(), settings, 8));
    }

    /// <summary>Proves invalid Unicode is rejected rather than persisted as changed replay evidence.</summary>
    [Fact]
    public void Encode_WhenTextContainsUnpairedSurrogate_ThrowsEncoderFallback()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var request = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null), [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("\uD800")), new(8, 32, TimeSpan.FromMinutes(5)));
        _ = Should.Throw<System.Text.EncoderFallbackException>(() => SqliteBudgetLedgerCodec.Encode(request, settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves arbitrary exact aggregate coefficients round-trip symmetrically through result evidence.</summary>
    [Fact]
    public void RoundTrip_WhenFailureContainsLargeExactQuantity_PreservesEveryDigit()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        var reference = new BudgetOverrunHoldReference(scope, reservation, new(1));
        var quantity = new BudgetQuantity(System.Numerics.BigInteger.Pow(10, 20_000) + 123, 28);
        var failure = new BudgetLimitFailure(scope.Id, new("test.sum"), BudgetLimitKind.Hard, decimal.MaxValue, quantity, quantity, new("count"), "bounded failure");
        BudgetOverrunHoldResolutionResult value = new BudgetOverrunHoldResolutionBlocked(reference, [], [failure]);
        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes);
        var decoded = SqliteBudgetLedgerCodec.Decode<BudgetOverrunHoldResolutionResult>(payload, settings, settings.MaximumResultBytes);
        decoded.ShouldBe(value);
    }

    /// <summary>Proves budget cardinality settings do not constrain unrelated security evidence collections.</summary>
    [Fact]
    public void EnforcementRoundTrip_WhenBudgetCardinalityIsOne_PreservesMultipleSecurityValues()
    {
        var defaults = SqliteBudgetLedgerSettings.CreateDefault();
        var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, defaults.MaximumPayloadBytes, defaults.MaximumResultBytes, 1, 1, defaults.MaximumLineageDepth);
        var claims = ImmutableArray.Create(new IdentityClaim(new("issuer"), "role", "operator", IdentityClaimValueKind.Text), new IdentityClaim(new("issuer"), "team", "budget", IdentityClaimValueKind.Text));
        var evidence = new AuthenticationEvidence(new("evidence"), new("issuer"), "test", DateTimeOffset.UnixEpoch, null, new(new ContentHash("fingerprint")));
        var delegation = new DelegationIdentityLink(new(Guid.NewGuid()), new("tenant"), new("delegator"), new("issuer"), evidence.Id, new(1), DateTimeOffset.UnixEpoch, claims, IdentityAssuranceLevel.Basic);
        var secondDelegation = new DelegationIdentityLink(new(Guid.NewGuid()), new("tenant"), new("delegator"), new("issuer"), evidence.Id, new(1), DateTimeOffset.UnixEpoch, claims, IdentityAssuranceLevel.Basic);
        var identity = new ExecutionIdentity(new("tenant"), new("operator"), ExecutionSubjectKind.Human, evidence, claims, [delegation, secondDelegation], IdentityAssuranceLevel.Basic, new(1));
        var scope = new SecurityAuthorizationScope(new(Guid.NewGuid()), null, new BeforeRunOperationCorrelation(new(Guid.NewGuid()), null));
        var request = new SecurityEnforcementRequest(scope, identity, new("budget-ledger"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new(ProtectedResourceKind.ApplicationState, "one"), new(ProtectedResourceKind.ApplicationState, "two")], new("input"), new(1));
        var payload = SqliteBudgetSecurityCodec.EncodeEnforcement(request, settings);
        var decoded = SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings);
        decoded.ShouldBe(request);
    }

    /// <summary>Proves an optional session, admission-caused correlation, captured authorization context, and an
    /// expiring authentication evidence all round-trip through the enforcement envelope.</summary>
    [Fact]
    public void EnforcementRoundTrip_WhenSessionAdmissionAndAuthorizationArePresent_PreservesEveryOptionalValue()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var claims = ImmutableArray.Create(new IdentityClaim(new("issuer"), "role", "operator", IdentityClaimValueKind.Text));
        var evidence = new AuthenticationEvidence(new("evidence"), new("issuer"), "test", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new(new ContentHash("fingerprint")));
        var identity = new ExecutionIdentity(new("tenant"), new("operator"), ExecutionSubjectKind.Human, evidence, claims, [], IdentityAssuranceLevel.Basic, new(1));
        var scope = new SecurityAuthorizationScope(new(Guid.NewGuid()), new(Guid.NewGuid()), new BeforeRunOperationCorrelation(new(Guid.NewGuid()), new(Guid.NewGuid())));
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("profile"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(new(Guid.NewGuid()), new(1), new ContentHash("policy-fingerprint")),
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1),
            scope, identity);
        var request = new SecurityEnforcementRequest(scope, identity, authorization, new("budget-ledger"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new(ProtectedResourceKind.ApplicationState, "one")], new("input"), new(1));

        var payload = SqliteBudgetSecurityCodec.EncodeEnforcement(request, settings);
        var decoded = SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings);

        decoded.ShouldBe(request);
    }

    /// <summary>Proves an in-run correlation with an active turn round-trips through the enforcement envelope.</summary>
    [Fact]
    public void EnforcementRoundTrip_WhenCorrelationIsInRun_PreservesRunAndTurn()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var evidence = new AuthenticationEvidence(new("evidence"), new("issuer"), "test", DateTimeOffset.UnixEpoch, null, new(new ContentHash("fingerprint")));
        var identity = new ExecutionIdentity(new("tenant"), new("operator"), ExecutionSubjectKind.Human, evidence, [], [], IdentityAssuranceLevel.Basic, new(1));
        var scope = new SecurityAuthorizationScope(new(Guid.NewGuid()), null, new InRunOperationCorrelation(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid())));
        var request = new SecurityEnforcementRequest(scope, identity, new("budget-ledger"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new(ProtectedResourceKind.ApplicationState, "one")], new("input"), new(1));

        var payload = SqliteBudgetSecurityCodec.EncodeEnforcement(request, settings);
        var decoded = SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings);

        decoded.ShouldBe(request);
    }

    /// <summary>Proves an after-run correlation to an already-settled causal run round-trips through the enforcement envelope.</summary>
    [Fact]
    public void EnforcementRoundTrip_WhenCorrelationIsAfterRun_PreservesCausalRun()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var evidence = new AuthenticationEvidence(new("evidence"), new("issuer"), "test", DateTimeOffset.UnixEpoch, null, new(new ContentHash("fingerprint")));
        var identity = new ExecutionIdentity(new("tenant"), new("operator"), ExecutionSubjectKind.Human, evidence, [], [], IdentityAssuranceLevel.Basic, new(1));
        var scope = new SecurityAuthorizationScope(new(Guid.NewGuid()), null, new AfterRunOperationCorrelation(new(Guid.NewGuid()), new(Guid.NewGuid())));
        var request = new SecurityEnforcementRequest(scope, identity, new("budget-ledger"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new(ProtectedResourceKind.ApplicationState, "one")], new("input"), new(1));

        var payload = SqliteBudgetSecurityCodec.EncodeEnforcement(request, settings);
        var decoded = SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings);

        decoded.ShouldBe(request);
    }

    /// <summary>Proves a nondefault GUID round-trips exactly through the RFC 4122 network-order codec pair.</summary>
    [Fact]
    public void GuidCodec_WhenValueRoundTrips_PreservesExactValue()
    {
        var value = Guid.NewGuid();

        var encoded = SqliteBudgetSecurityCodec.EncodeGuid(value);
        var decoded = SqliteBudgetSecurityCodec.DecodeGuid(encoded);

        encoded.Length.ShouldBe(16);
        decoded.ShouldBe(value);
    }

    /// <summary>Proves a value that is not exactly sixteen bytes is rejected as invalid persisted evidence.</summary>
    [Fact]
    public void DecodeGuid_WhenValueIsNotSixteenBytes_ThrowsInvalidData() =>
        Should.Throw<InvalidDataException>(() => SqliteBudgetSecurityCodec.DecodeGuid(new byte[8]));

    /// <summary>Proves an empty enforcement payload is rejected before decoding begins.</summary>
    [Fact]
    public void DecodeEnforcement_WhenPayloadIsEmpty_ThrowsInvalidData() =>
        Should.Throw<InvalidDataException>(() => SqliteBudgetSecurityCodec.DecodeEnforcement([], SqliteBudgetLedgerSettings.CreateDefault()));

    /// <summary>Proves a payload beyond the configured bound is rejected before decoding begins.</summary>
    [Fact]
    public void DecodeEnforcement_WhenPayloadExceedsConfiguredBound_ThrowsInvalidData()
    {
        var defaults = SqliteBudgetLedgerSettings.CreateDefault();
        var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, 4, defaults.MaximumResultBytes, defaults.MaximumBatchSize, defaults.MaximumLimitsPerScope, defaults.MaximumLineageDepth);

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetSecurityCodec.DecodeEnforcement(new byte[8], settings));
    }

    /// <summary>Proves an enforcement payload with a structurally valid header but an out-of-range decoded
    /// timestamp fails closed as persisted-data corruption rather than an uncontained argument exception.</summary>
    [Fact]
    public void DecodeEnforcement_WhenDomainContentIsInvalid_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var evidence = new AuthenticationEvidence(new("evidence"), new("issuer"), "test", DateTimeOffset.UnixEpoch, null, new(new ContentHash("fingerprint")));
        var identity = new ExecutionIdentity(new("tenant"), new("operator"), ExecutionSubjectKind.Human, evidence, [], [], IdentityAssuranceLevel.Basic, new(1));
        var scope = new SecurityAuthorizationScope(new(Guid.NewGuid()), null, new BeforeRunOperationCorrelation(new(Guid.NewGuid()), null));
        var request = new SecurityEnforcementRequest(scope, identity, new("budget-ledger"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new(ProtectedResourceKind.ApplicationState, "one")], new("input"), new(1));
        var payload = SqliteBudgetSecurityCodec.EncodeEnforcement(request, settings).ToArray();

        // Locate the unique encoded AuthenticatedAt.Ticks field (DateTimeOffset.UnixEpoch appears exactly once
        // in this request) and corrupt it to an out-of-range tick count, so reconstructing the DateTimeOffset
        // throws ArgumentOutOfRangeException, which the codec wraps as InvalidDataException.
        var ticksBytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(ticksBytes, DateTimeOffset.UnixEpoch.Ticks);
        var index = IndexOfSequence(payload, ticksBytes);
        index.ShouldBeGreaterThanOrEqualTo(0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(index), long.MaxValue);

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetSecurityCodec.DecodeEnforcement(payload, settings));
    }

    private static int IndexOfSequence(byte[] haystack, byte[] needle)
    {
        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            var found = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (haystack[start + offset] != needle[offset])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return start;
            }
        }

        return -1;
    }

    /// <summary>Proves an unsupported evidence type is rejected before any bytes are produced.</summary>
    [Fact]
    public void Encode_WhenValueTypeIsUnsupported_ThrowsArgumentException()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        _ = Should.Throw<ArgumentException>(() => SqliteBudgetLedgerCodec.Encode("not supported", settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves an empty payload is rejected before decoding begins.</summary>
    [Fact]
    public void Decode_WhenPayloadIsEmpty_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>([], settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves a structurally valid envelope whose kind does not match the requested type is rejected.</summary>
    [Fact]
    public void Decode_WhenKindDoesNotMatchRequestedType_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var payload = SqliteBudgetLedgerCodec.Encode(Request(), settings, settings.MaximumPayloadBytes);
        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerBatchReserveRequest>(payload, settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves a correction result with created and cleared overrun holds round-trips exactly.</summary>
    [Fact]
    public void RoundTrip_WhenCorrectionHasCreatedAndClearedHolds_PreservesEveryHold()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        var holdReference = new BudgetOverrunHoldReference(scope, reservation, new(1));
        var hold = new BudgetOverrunHold(holdReference, new("test.sum"), new("count"), 1m, 2m, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var clearedReference = new BudgetOverrunHoldReference(scope, reservation, new(2));
        BudgetCorrectionResult value = new(reservation.Id, 2m, 1m, 1, new(3), [hold], [clearedReference]);

        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes);
        var decoded = SqliteBudgetLedgerCodec.Decode<BudgetCorrectionResult>(payload, settings, settings.MaximumResultBytes);

        decoded.ShouldBe(value);
    }

    /// <summary>Proves every concrete reconciliation-evidence kind round-trips exactly.</summary>
    [Theory]
    [MemberData(nameof(ReconciliationEvidenceCases))]
    public void RoundTrip_WhenReconciliationEvidenceVaries_PreservesExactKind(BudgetReconciliationEvidence value)
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();

        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes);
        var decoded = SqliteBudgetLedgerCodec.Decode<BudgetReconciliationEvidence>(payload, settings, settings.MaximumResultBytes);

        decoded.ShouldBe(value);
    }

    public static TheoryData<BudgetReconciliationEvidence> ReconciliationEvidenceCases =>
    [
        new BudgetActualMeasured(1m),
        new BudgetActualEstimated(2m),
        new BudgetNoUsageProven(),
        new BudgetStillUnknown(),
    ];

    /// <summary>Proves every concrete reconciliation-result kind round-trips exactly.</summary>
    [Fact]
    public void RoundTrip_WhenReconciliationResultIsReleasedOrRetainedUnknown_PreservesExactKind()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));

        BudgetLedgerReconciliationResult released = new BudgetLedgerReconciliationReleased(reservation);
        var releasedPayload = SqliteBudgetLedgerCodec.Encode(released, settings, settings.MaximumResultBytes);
        SqliteBudgetLedgerCodec.Decode<BudgetLedgerReconciliationResult>(releasedPayload, settings, settings.MaximumResultBytes).ShouldBe(released);

        BudgetLedgerReconciliationResult retainedUnknown = new BudgetLedgerReconciliationRetainedUnknown(reservation);
        var retainedPayload = SqliteBudgetLedgerCodec.Encode(retainedUnknown, settings, settings.MaximumResultBytes);
        SqliteBudgetLedgerCodec.Decode<BudgetLedgerReconciliationResult>(retainedPayload, settings, settings.MaximumResultBytes).ShouldBe(retainedUnknown);
    }

    /// <summary>Proves an unknown persisted reconciliation-evidence kind byte is rejected.</summary>
    [Fact]
    public void Decode_WhenReconciliationEvidenceKindByteIsUnknown_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var payload = SqliteBudgetLedgerCodec.Encode<BudgetReconciliationEvidence>(new BudgetStillUnknown(), settings, settings.MaximumResultBytes).ToArray();
        payload[^1] = 99;

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetReconciliationEvidence>(payload, settings, settings.MaximumResultBytes));
    }

    /// <summary>Proves an unknown persisted reconciliation-result kind byte is rejected.</summary>
    [Fact]
    public void Decode_WhenReconciliationResultKindByteIsUnknown_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        BudgetLedgerReconciliationResult value = new BudgetLedgerReconciliationReleased(reservation);
        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes).ToArray();
        payload[6] = 99;

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerReconciliationResult>(payload, settings, settings.MaximumResultBytes));
    }

    /// <summary>Proves an unknown persisted overrun-resolution result kind byte is rejected.</summary>
    [Fact]
    public void Decode_WhenResolutionResultKindByteIsUnknown_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var value = ResolutionBlocked();
        var payload = SqliteBudgetLedgerCodec.Encode<BudgetOverrunHoldResolutionResult>(value, settings, settings.MaximumResultBytes).ToArray();
        payload[6] = 99;

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetOverrunHoldResolutionResult>(payload, settings, settings.MaximumResultBytes));
    }

    /// <summary>Proves a blocked resolution with a nonempty current-overruns collection round-trips exactly.</summary>
    [Fact]
    public void RoundTrip_WhenResolutionBlockedHasCurrentOverruns_PreservesOverruns()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var value = ResolutionBlocked();

        var payload = SqliteBudgetLedgerCodec.Encode<BudgetOverrunHoldResolutionResult>(value, settings, settings.MaximumResultBytes);
        var decoded = SqliteBudgetLedgerCodec.Decode<BudgetOverrunHoldResolutionResult>(payload, settings, settings.MaximumResultBytes);

        decoded.ShouldBe(value);
    }

    /// <summary>Proves a resolved outcome carrying a required distributed fence round-trips exactly.</summary>
    [Fact]
    public void RoundTrip_WhenResolvedEnforcementReceiptHasRequiredFence_PreservesFence()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        var holdReference = new BudgetOverrunHoldReference(scope, reservation, new(1));
        var receipt = EnforcementReceipt(holdReference, requiredFence: new FencingToken(7));
        BudgetOverrunHoldResolutionResult value = new BudgetOverrunHoldResolved(holdReference, new(2), receipt);

        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes);
        var decoded = SqliteBudgetLedgerCodec.Decode<BudgetOverrunHoldResolutionResult>(payload, settings, settings.MaximumResultBytes);

        decoded.ShouldBe(value);
    }

    /// <summary>Proves a persisted string with a negative declared length is rejected.</summary>
    [Fact]
    public void Decode_WhenStringLengthIsNegative_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var payload = SqliteBudgetLedgerCodec.Encode(Request(), settings, settings.MaximumPayloadBytes).ToArray();
        var lengthOffset = IndexOfSequence(payload, BitConverter.GetBytes(5));
        lengthOffset.ShouldBeGreaterThanOrEqualTo(0);
        BitConverter.GetBytes(-1).CopyTo(payload, lengthOffset);

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetLedgerScopeCreateRequest>(payload, settings, settings.MaximumPayloadBytes));
    }

    /// <summary>Proves a persisted exact quantity with a negative declared coefficient length is rejected.</summary>
    [Fact]
    public void Decode_WhenQuantityCoefficientLengthIsNegative_ThrowsInvalidData()
    {
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        var holdReference = new BudgetOverrunHoldReference(scope, reservation, new(1));
        var quantity = BudgetQuantity.FromDecimal(123456m);
        var failure = new BudgetLimitFailure(scope.Id, new("test.sum"), BudgetLimitKind.Hard, 1m, quantity, quantity, new("count"), "bounded failure");
        BudgetOverrunHoldResolutionResult value = new BudgetOverrunHoldResolutionBlocked(holdReference, [], [failure]);
        var payload = SqliteBudgetLedgerCodec.Encode(value, settings, settings.MaximumResultBytes).ToArray();
        var coefficientByteCount = quantity.Coefficient.GetByteCount(isUnsigned: true);
        var lengthOffset = IndexOfSequence(payload, BitConverter.GetBytes(coefficientByteCount));
        lengthOffset.ShouldBeGreaterThanOrEqualTo(0);
        BitConverter.GetBytes(-1).CopyTo(payload, lengthOffset);

        _ = Should.Throw<InvalidDataException>(() => SqliteBudgetLedgerCodec.Decode<BudgetOverrunHoldResolutionResult>(payload, settings, settings.MaximumResultBytes));
    }

    private static BudgetOverrunHoldResolutionBlocked ResolutionBlocked()
    {
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(scope, new(Guid.NewGuid()));
        var holdReference = new BudgetOverrunHoldReference(scope, reservation, new(1));
        var overrun = new BudgetOverrunHold(holdReference, new("test.sum"), new("count"), 1m, 2m, BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var failure = new BudgetLimitFailure(scope.Id, new("test.sum"), BudgetLimitKind.Hard, 10m, BudgetQuantity.FromDecimal(2), BudgetQuantity.FromDecimal(1), new("count"), "blocked");
        return new(holdReference, [overrun], [failure]);
    }

    private static SecurityEnforcementIntentReceipt EnforcementReceipt(BudgetOverrunHoldReference hold, FencingToken? requiredFence)
    {
        var enforcement = new SecurityEnforcementRequest(
            new SecurityAuthorizationScope(hold.Boundary.Address.AgentId, null, new BeforeRunOperationCorrelation(new(Guid.NewGuid()), null)),
            TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human),
            new ComponentId("budget-operator"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            [BudgetOverrunSecurityBinding.Resource(hold)], BudgetOverrunSecurityBinding.Fingerprint(hold), new SecurityRevocationVersion(1));
        return new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()), enforcement, requiredFence, new ContentHash("sha256:codec-test"), DateTimeOffset.UnixEpoch);
    }

    private static BudgetLedgerScopeCreateRequest Request() => new(new BudgetScopeRequest(null, new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null), [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("codec")), new(8, 32, TimeSpan.FromMinutes(5)));
}
