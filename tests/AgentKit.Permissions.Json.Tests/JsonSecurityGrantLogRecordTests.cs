// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies every JsonSecurityGrantLogRecord factory, its argument guards, and its JSON round trip.</summary>
public sealed class JsonSecurityGrantLogRecordTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a registration record carries the projected grant and no other member.</summary>
    [Fact]
    public void ForRegistration_WhenGrantIsValid_CarriesOnlyGrant()
    {
        var grant = TestGrantFactory.CreateGrant(_now);

        var record = JsonSecurityGrantLogRecord.ForRegistration(grant);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.Registered);
        record.Grant.ShouldBe(JsonSecurityGrant.FromDomain(grant));
        record.GrantId.ShouldBeNull();
        record.RemainingUses.ShouldBeNull();
        record.Revoked.ShouldBeNull();
        record.Receipt.ShouldBeNull();
    }

    /// <summary>Verifies a null grant is rejected before a record is built.</summary>
    [Fact]
    public void ForRegistration_WhenGrantIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(
            static () => JsonSecurityGrantLogRecord.ForRegistration(null!));

        exception.ParamName.ShouldBe("grant");
    }

    /// <summary>Verifies a receiptless consumption record carries only the grant identity and remaining uses.</summary>
    [Fact]
    public void ForConsumption_WhenReceiptIsNull_CarriesOnlyGrantIdAndRemainingUses()
    {
        var grantId = new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005"));

        var record = JsonSecurityGrantLogRecord.ForConsumption(grantId, 3, null);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.Consumed);
        record.GrantId.ShouldBe(grantId.Value);
        record.RemainingUses.ShouldBe(3);
        record.Grant.ShouldBeNull();
        record.Revoked.ShouldBeNull();
        record.Receipt.ShouldBeNull();
    }

    /// <summary>Verifies a receipt-bearing consumption record projects the receipt alongside the remaining uses.</summary>
    [Fact]
    public void ForConsumption_WhenReceiptIsPresent_ProjectsReceipt()
    {
        var grant = TestGrantFactory.CreateGrant(_now);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var intent = TestGrantFactory.CreateIntent();
        var receipt = TestGrantFactory.CreateReceipt(grant, enforcement, intent, _now);

        var record = JsonSecurityGrantLogRecord.ForConsumption(grant.Id, 1, receipt);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.Consumed);
        record.GrantId.ShouldBe(grant.Id.Value);
        record.RemainingUses.ShouldBe(1);
        record.Receipt.ShouldBe(JsonSecurityEnforcementIntentReceipt.FromDomain(receipt));
    }

    /// <summary>Verifies negative remaining uses are rejected with the exact parameter name.</summary>
    [Fact]
    public void ForConsumption_WhenRemainingUsesIsNegative_ThrowsArgumentOutOfRange()
    {
        var grantId = new GrantId(Guid.NewGuid());

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => JsonSecurityGrantLogRecord.ForConsumption(grantId, -1, null));

        exception.ParamName.ShouldBe("remainingUses");
    }

    /// <summary>Verifies a revocation record carries only the grant identity.</summary>
    [Fact]
    public void ForRevocation_WhenCalled_CarriesOnlyGrantId()
    {
        var grantId = new GrantId(Guid.NewGuid());

        var record = JsonSecurityGrantLogRecord.ForRevocation(grantId);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.Revoked);
        record.GrantId.ShouldBe(grantId.Value);
        record.Grant.ShouldBeNull();
        record.RemainingUses.ShouldBeNull();
        record.Revoked.ShouldBeNull();
        record.Receipt.ShouldBeNull();
    }

    /// <summary>Verifies a compaction state record carries the grant identity, remaining uses, and revocation flag.</summary>
    [Fact]
    public void ForState_WhenCalled_CarriesGrantIdRemainingUsesAndRevoked()
    {
        var grantId = new GrantId(Guid.NewGuid());

        var record = JsonSecurityGrantLogRecord.ForState(grantId, 2, true);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.State);
        record.GrantId.ShouldBe(grantId.Value);
        record.RemainingUses.ShouldBe(2);
        record.Revoked.ShouldBe(true);
        record.Grant.ShouldBeNull();
        record.Receipt.ShouldBeNull();
    }

    /// <summary>Verifies negative remaining uses are rejected for a compaction state record with the exact parameter name.</summary>
    [Fact]
    public void ForState_WhenRemainingUsesIsNegative_ThrowsArgumentOutOfRange()
    {
        var grantId = new GrantId(Guid.NewGuid());

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => JsonSecurityGrantLogRecord.ForState(grantId, -1, false));

        exception.ParamName.ShouldBe("remainingUses");
    }

    /// <summary>Verifies a compaction receipt record carries only the projected receipt.</summary>
    [Fact]
    public void ForReceipt_WhenReceiptIsValid_CarriesOnlyReceipt()
    {
        var grant = TestGrantFactory.CreateGrant(_now);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var intent = TestGrantFactory.CreateIntent();
        var receipt = TestGrantFactory.CreateReceipt(grant, enforcement, intent, _now);

        var record = JsonSecurityGrantLogRecord.ForReceipt(receipt);

        record.Kind.ShouldBe(JsonSecurityGrantLogRecordKind.Receipt);
        record.Receipt.ShouldBe(JsonSecurityEnforcementIntentReceipt.FromDomain(receipt));
        record.Grant.ShouldBeNull();
        record.GrantId.ShouldBeNull();
        record.RemainingUses.ShouldBeNull();
        record.Revoked.ShouldBeNull();
    }

    /// <summary>Verifies a null receipt is rejected before a compaction receipt record is built.</summary>
    [Fact]
    public void ForReceipt_WhenReceiptIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(
            static () => JsonSecurityGrantLogRecord.ForReceipt(null!));

        exception.ParamName.ShouldBe("receipt");
    }

    /// <summary>Verifies a registration record survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenRegistration_RoundTripsExactly()
    {
        var grant = TestGrantFactory.CreateGrant(_now, identity: TestGrantFactory.CreateRichIdentity(_now));
        var record = JsonSecurityGrantLogRecord.ForRegistration(grant);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, options);
        var decoded = JsonSerializer.Deserialize<JsonSecurityGrantLogRecord>(bytes, options);

        decoded.ShouldBe(record);
    }

    /// <summary>Verifies a receipt-bearing consumption record survives a real JSON encode and decode round trip.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenConsumptionWithReceipt_RoundTripsExactly()
    {
        var grant = TestGrantFactory.CreateGrant(_now);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var intent = TestGrantFactory.CreateIntent(fence: new FencingToken(3));
        var receipt = TestGrantFactory.CreateReceipt(grant, enforcement, intent, _now);
        var record = JsonSecurityGrantLogRecord.ForConsumption(grant.Id, 1, receipt);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, options);
        var decoded = JsonSerializer.Deserialize<JsonSecurityGrantLogRecord>(bytes, options);

        decoded.ShouldBe(record);
    }
}
