// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror of the complete immutable evidence one scope was admitted under.</summary>
/// <remarks>
/// <see cref="JsonBudgetScopeCreateRequest"/> overrides <see cref="object.Equals(object?)"/> and
/// <see cref="object.GetHashCode"/> to compare its ordered limit sequence element-wise rather than by backing-array
/// identity, because two documents decoded from byte-identical JSON otherwise carry distinct <see cref="ImmutableArray{T}"/>
/// instances. These cases exercise every branch of that custom comparison.
/// </remarks>
public sealed class JsonBudgetScopeCreateRequestTests
{
    private static readonly JsonBudgetScopeAddress _address = new("tenant", "principal", Guid.NewGuid(), null, null, null);
    private static readonly JsonBudgetScopeAdmission _admission = new(
        8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled);

    /// <summary>Verifies projecting a null domain request is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetScopeCreateRequest.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a round trip through <see cref="JsonBudgetScopeCreateRequest.FromDomain"/> and <see cref="JsonBudgetScopeCreateRequest.ToDomain"/> reproduces an equal domain value, including limit order.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_RoundTripsExactlyIncludingLimitOrder()
    {
        var original = CreateDomainRequest(
            new BudgetLimit(new BudgetDimension("a"), 1, new BudgetUnit("count"), BudgetLimitKind.Hard),
            new BudgetLimit(new BudgetDimension("b"), 2, new BudgetUnit("count"), BudgetLimitKind.Soft));

        var restored = JsonBudgetScopeCreateRequest.FromDomain(original).ToDomain();

        restored.ShouldBe(original);
        restored.OriginalRequest.Limits.Select(item => item.Dimension.Value).ShouldBe(["a", "b"]);
    }

    /// <summary>Verifies a request with no configured limits round-trips through the empty-array normalization.</summary>
    [Fact]
    public void ToDomain_WhenNoLimitsAreConfigured_RoundTripsWithEmptyLimits()
    {
        var original = CreateDomainRequest();

        var document = JsonBudgetScopeCreateRequest.FromDomain(original);

        document.ConfiguredLimits.ShouldBeEmpty();
        document.ToDomain().OriginalRequest.Limits.ShouldBeEmpty();
    }

    /// <summary>Verifies a document whose <see cref="JsonBudgetScopeCreateRequest.Limits"/> member is a default (never-assigned) array normalizes to empty rather than throwing.</summary>
    [Fact]
    public void ConfiguredLimits_WhenLimitsIsDefaultArray_NormalizesToEmpty()
    {
        var document = new JsonBudgetScopeCreateRequest(null, _address, default, "key", _admission);

        document.ConfiguredLimits.ShouldBeEmpty();
    }

    /// <summary>Verifies two documents with identical scalar members and limits in the same order compare equal.</summary>
    [Fact]
    public void Equals_WhenEveryFieldAndLimitOrderMatch_ReportsEquality()
    {
        var first = CreateDocument(new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard));
        var second = CreateDocument(new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard));

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    /// <summary>Verifies documents whose limit sequences differ only in length are not equal.</summary>
    [Fact]
    public void Equals_WhenLimitSequenceLengthsDiffer_ReportsInequality()
    {
        var first = CreateDocument(new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard));
        var second = CreateDocument(
            new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard),
            new JsonBudgetLimit("b", 2, "count", BudgetLimitKind.Soft));

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies documents whose limit sequences contain the same members in a different order are not equal.</summary>
    [Fact]
    public void Equals_WhenLimitOrderDiffers_ReportsInequality()
    {
        var first = CreateDocument(
            new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard),
            new JsonBudgetLimit("b", 2, "count", BudgetLimitKind.Soft));
        var second = CreateDocument(
            new JsonBudgetLimit("b", 2, "count", BudgetLimitKind.Soft),
            new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard));

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies a document with a default (never-assigned) limits array compares equal to one with an explicit empty array.</summary>
    [Fact]
    public void Equals_WhenOneLimitsArrayIsDefaultAndOtherIsEmpty_ReportsEquality()
    {
        var withDefault = new JsonBudgetScopeCreateRequest(null, _address, default, "key", _admission);
        var withEmpty = new JsonBudgetScopeCreateRequest(null, _address, [], "key", _admission);

        withEmpty.ShouldBe(withDefault);
        withEmpty.GetHashCode().ShouldBe(withDefault.GetHashCode());
    }

    /// <summary>Verifies documents with different parent scope identities are not equal.</summary>
    [Fact]
    public void Equals_WhenParentScopeIdDiffers_ReportsInequality()
    {
        var first = new JsonBudgetScopeCreateRequest(Guid.NewGuid(), _address, [], "key", _admission);
        var second = new JsonBudgetScopeCreateRequest(Guid.NewGuid(), _address, [], "key", _admission);

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies documents with different replay keys are not equal.</summary>
    [Fact]
    public void Equals_WhenIdempotencyKeyDiffers_ReportsInequality()
    {
        var first = new JsonBudgetScopeCreateRequest(null, _address, [], "first-key", _admission);
        var second = new JsonBudgetScopeCreateRequest(null, _address, [], "second-key", _admission);

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies comparison against null reports inequality without throwing.</summary>
    [Fact]
    public void Equals_WhenOtherIsNull_ReportsInequality()
    {
        var document = new JsonBudgetScopeCreateRequest(null, _address, [], "key", _admission);

        document.Equals(null).ShouldBeFalse();
    }

    /// <summary>Verifies a null <see cref="JsonBudgetScopeCreateRequest.Address"/> is refused during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var document = new JsonBudgetScopeCreateRequest(null, null!, [], "key", _admission);

        Should.Throw<ArgumentNullException>(document.ToDomain).ParamName.ShouldBe("Address");
    }

    /// <summary>Verifies a null <see cref="JsonBudgetScopeCreateRequest.Admission"/> is refused during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenAdmissionIsNull_ThrowsArgumentNullException()
    {
        var document = new JsonBudgetScopeCreateRequest(null, _address, [], "key", null!);

        Should.Throw<ArgumentNullException>(document.ToDomain).ParamName.ShouldBe("Admission");
    }

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip, including nested address, limits, and admission.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = CreateDocument(
            new JsonBudgetLimit("a", 1, "count", BudgetLimitKind.Hard),
            new JsonBudgetLimit("b", 2, "bytes", BudgetLimitKind.Soft));
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetScopeCreateRequest>(
            JsonStoreSerialization.Encode(original, options, 4_096), options);

        decoded.ShouldBe(original);
    }

    private static readonly Guid _parentScopeId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static JsonBudgetScopeCreateRequest CreateDocument(params JsonBudgetLimit[] limits) =>
        new(_parentScopeId, _address, [.. limits], "key", _admission);

    private static BudgetLedgerScopeCreateRequest CreateDomainRequest(params BudgetLimit[] limits) =>
        new(
            new BudgetScopeRequest(
                null,
                new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null),
                [.. limits],
                new IdempotencyKey("scope-create-request-key")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled));
}
