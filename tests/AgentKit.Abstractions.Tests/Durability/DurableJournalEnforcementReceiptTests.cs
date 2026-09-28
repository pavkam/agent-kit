// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableJournalEnforcementReceipt"/> behavior and contracts.</summary>
public sealed class DurableJournalEnforcementReceiptTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreSupplied_RoundTripsProperties()
    {
        var grantId = new GrantId(Guid.Parse("a2000000-0000-0000-0000-000000000001"));
        var intentId = new SecurityEnforcementIntentId(Guid.Parse("b2000000-0000-0000-0000-000000000002"));
        var auditId = new SecurityAuditRecordId(Guid.Parse("c2000000-0000-0000-0000-000000000003"));

        var receipt = new DurableJournalEnforcementReceipt(grantId, intentId, auditId, DurabilityTestData.Now);

        receipt.GrantId.ShouldBe(grantId);
        receipt.IntentId.ShouldBe(intentId);
        receipt.AuditRecordId.ShouldBe(auditId);
        receipt.ConsumedAt.ShouldBe(DurabilityTestData.Now);
    }

    [Fact]
    public void Constructor_WhenAuditIsAbsent_RetainsNullWithoutFabricatingAnIdentity()
    {
        var receipt = new DurableJournalEnforcementReceipt(
            new GrantId(Guid.Parse("a2000000-0000-0000-0000-000000000001")),
            new SecurityEnforcementIntentId(Guid.Parse("b2000000-0000-0000-0000-000000000002")),
            null,
            DurabilityTestData.Now);

        receipt.AuditRecordId.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenConsumptionInstantDiffers_ReturnsFalse()
    {
        var receipt = new DurableJournalEnforcementReceipt(
            new GrantId(Guid.Parse("a2000000-0000-0000-0000-000000000001")),
            new SecurityEnforcementIntentId(Guid.Parse("b2000000-0000-0000-0000-000000000002")),
            null,
            DurabilityTestData.Now);

        receipt.ShouldNotBe(receipt with { ConsumedAt = DurabilityTestData.Now.AddTicks(1) });
    }
}
