// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies JsonBudgetLedgerTarget behavior and contracts.</summary>
/// <remarks>
/// The target is the trusted bootstrap boundary for one durable ledger root. Every case here proves that an invalid
/// combination is refused by construction, before any filesystem effect could be attributed to a misconfigured host.
/// </remarks>
public sealed class JsonBudgetLedgerTargetTests
{
    private static readonly JsonBudgetLedgerInstanceId _storeId =
        new(Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"));

    /// <summary>Verifies a null root path is attributed to its exact parameter before any other validation runs.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new JsonBudgetLedgerTarget(
            null!, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("directoryPath");

    /// <summary>Verifies an empty root path is refused as a blank configuration value.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsEmpty_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new JsonBudgetLedgerTarget(
            string.Empty, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("directoryPath");

    /// <summary>Verifies a whitespace-only root path is refused as a blank configuration value.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsWhitespace_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new JsonBudgetLedgerTarget(
            "   ", _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("directoryPath");

    /// <summary>Verifies a relative root path is refused, because the effective root would depend on the process directory.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsRelative_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonBudgetLedgerTarget(
            Path.Combine("relative", "ledger"),
            _storeId,
            JsonStoreOpenMode.OpenExisting,
            JsonStoreRecoveryMode.ValidateExact));

        exception.ParamName.ShouldBe("directoryPath");
        exception.ShouldNotBeOfType<ArgumentNullException>();
    }

    /// <summary>Verifies a default store identity is refused so a root can never bind to an unnamed deployment.</summary>
    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-default-id"),
            default,
            JsonStoreOpenMode.OpenExisting,
            JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("expectedStoreInstanceId");

    /// <summary>Verifies an out-of-range open mode is refused rather than silently treated as one of the defined modes.</summary>
    [Fact]
    public void Constructor_WhenOpenModeIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-undefined-open"),
            _storeId,
            (JsonStoreOpenMode) 9_999,
            JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("openMode");

    /// <summary>Verifies an out-of-range recovery mode is refused rather than silently treated as validation only.</summary>
    [Fact]
    public void Constructor_WhenRecoveryModeIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-undefined-recovery"),
            _storeId,
            JsonStoreOpenMode.OpenExisting,
            (JsonStoreRecoveryMode) 9_999))
            .ParamName.ShouldBe("recoveryMode");

    /// <summary>Verifies creation permission combined with validation-only recovery is refused and attributed to the recovery mode.</summary>
    [Fact]
    public void Constructor_WhenCreateIfMissingCombinedWithValidateExact_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-create-validate"),
            _storeId,
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("recoveryMode");

    /// <summary>Verifies every other open and recovery combination is accepted and captured exactly.</summary>
    /// <param name="openMode">The declared root-creation permission.</param>
    /// <param name="recoveryMode">The declared torn-append policy.</param>
    [Theory]
    [InlineData(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)]
    [InlineData(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.RecoverTornAppends)]
    [InlineData(JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)]
    public void Constructor_WhenCombinationIsPermitted_ExposesExactCapturedValues(
        JsonStoreOpenMode openMode, JsonStoreRecoveryMode recoveryMode)
    {
        var path = Path.GetFullPath("target-permitted");

        var target = new JsonBudgetLedgerTarget(path, _storeId, openMode, recoveryMode);

        target.DirectoryPath.ShouldBe(path);
        target.ExpectedStoreInstanceId.ShouldBe(_storeId);
        target.OpenMode.ShouldBe(openMode);
        target.RecoveryMode.ShouldBe(recoveryMode);
    }

    /// <summary>Verifies a fully qualified but unnormalized path is stored in its canonical form.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsUnnormalized_StoresTheFullyResolvedPath()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "agentkit-budget-json-normalized"));
        var unnormalized = Path.Combine(Path.GetTempPath(), "agentkit-budget-json-nested", "..", "agentkit-budget-json-normalized");

        var target = new JsonBudgetLedgerTarget(
            unnormalized, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        target.DirectoryPath.ShouldBe(expected);
        target.DirectoryPath.ShouldNotBe(unnormalized);
    }

    /// <summary>Verifies two targets describing the same bootstrap facts compare equal and hash alike.</summary>
    [Fact]
    public void Equals_WhenEveryCapturedFactMatches_ReportsValueEquality()
    {
        var path = Path.GetFullPath("target-equality");
        var first = new JsonBudgetLedgerTarget(
            path, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);
        var second = new JsonBudgetLedgerTarget(
            path, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
        second.ToString().ShouldBe(first.ToString());
    }

    /// <summary>Verifies a target with a different declared open mode is not equal to an otherwise identical one.</summary>
    [Fact]
    public void Equals_WhenOpenModeDiffers_ReportsInequality()
    {
        var path = Path.GetFullPath("target-inequality");
        var first = new JsonBudgetLedgerTarget(
            path, _storeId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.RecoverTornAppends);
        var second = new JsonBudgetLedgerTarget(
            path, _storeId, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        second.ShouldNotBe(first);
    }

    /// <summary>Verifies record cloning preserves every captured bootstrap fact.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var original = new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-with-expression"),
            _storeId,
            JsonStoreOpenMode.OpenExisting,
            JsonStoreRecoveryMode.ValidateExact);

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.DirectoryPath.ShouldBe(original.DirectoryPath);
        copy.ExpectedStoreInstanceId.ShouldBe(original.ExpectedStoreInstanceId);
    }

    /// <summary>Verifies the diagnostic text names the type so a composition failure identifies the offending configuration.</summary>
    [Fact]
    public void ToString_WhenCalled_NamesTheTargetType()
    {
        var target = new JsonBudgetLedgerTarget(
            Path.GetFullPath("target-to-string"),
            _storeId,
            JsonStoreOpenMode.OpenExisting,
            JsonStoreRecoveryMode.ValidateExact);

        target.ToString().ShouldStartWith(nameof(JsonBudgetLedgerTarget));
    }
}
