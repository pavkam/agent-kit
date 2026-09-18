// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the flattened discriminated document mirroring the closed reconciliation evidence hierarchy.</summary>
public sealed class JsonBudgetReconciliationEvidenceTests
{
    /// <summary>Verifies projecting a null domain evidence value is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetReconciliationEvidence.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies measured evidence round-trips its kind and quantity exactly.</summary>
    [Fact]
    public void RoundTrip_WhenEvidenceIsMeasured_PreservesKindAndQuantity()
    {
        var original = new BudgetActualMeasured(5);

        var document = JsonBudgetReconciliationEvidence.FromDomain(original);

        document.Kind.ShouldBe(JsonBudgetReconciliationEvidenceKind.Measured);
        document.Actual.ShouldBe(5);
        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies estimated evidence round-trips its kind and quantity exactly.</summary>
    [Fact]
    public void RoundTrip_WhenEvidenceIsEstimated_PreservesKindAndQuantity()
    {
        var original = new BudgetActualEstimated(3);

        var document = JsonBudgetReconciliationEvidence.FromDomain(original);

        document.Kind.ShouldBe(JsonBudgetReconciliationEvidenceKind.Estimated);
        document.Actual.ShouldBe(3);
        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies proven-no-usage evidence round-trips with no persisted quantity.</summary>
    [Fact]
    public void RoundTrip_WhenEvidenceIsNoUsageProven_OmitsQuantity()
    {
        var document = JsonBudgetReconciliationEvidence.FromDomain(new BudgetNoUsageProven());

        document.Kind.ShouldBe(JsonBudgetReconciliationEvidenceKind.NoUsageProven);
        document.Actual.ShouldBeNull();
        document.ToDomain().ShouldBe(new BudgetNoUsageProven());
    }

    /// <summary>Verifies still-unknown evidence round-trips with no persisted quantity.</summary>
    [Fact]
    public void RoundTrip_WhenEvidenceIsStillUnknown_OmitsQuantity()
    {
        var document = JsonBudgetReconciliationEvidence.FromDomain(new BudgetStillUnknown());

        document.Kind.ShouldBe(JsonBudgetReconciliationEvidenceKind.StillUnknown);
        document.Actual.ShouldBeNull();
        document.ToDomain().ShouldBe(new BudgetStillUnknown());
    }

    /// <summary>Verifies a measured document omitting its required quantity is rejected during reconstruction rather than silently settling with a fabricated zero.</summary>
    [Fact]
    public void ToDomain_WhenMeasuredQuantityIsOmitted_ThrowsArgumentException()
    {
        var document = new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.Measured, null);

        Should.Throw<ArgumentException>(document.ToDomain).ParamName.ShouldBe("Actual");
    }

    /// <summary>Verifies an estimated document omitting its required quantity is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenEstimatedQuantityIsOmitted_ThrowsArgumentException()
    {
        var document = new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.Estimated, null);

        Should.Throw<ArgumentException>(document.ToDomain).ParamName.ShouldBe("Actual");
    }

    /// <summary>Verifies an undefined persisted kind is rejected during reconstruction instead of defaulting to a known evidence category.</summary>
    [Fact]
    public void ToDomain_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonBudgetReconciliationEvidence((JsonBudgetReconciliationEvidenceKind) 9_999, null);

        Should.Throw<ArgumentOutOfRangeException>(document.ToDomain).ParamName.ShouldBe("Kind");
    }

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip for a quantity-bearing kind.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.Measured, 12);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetReconciliationEvidence>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
