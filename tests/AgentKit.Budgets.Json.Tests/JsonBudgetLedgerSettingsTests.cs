// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the immutable positive evidence bounds and encoding contract captured by one JSON budget ledger.</summary>
public sealed class JsonBudgetLedgerSettingsTests
{
    /// <summary>Verifies valid bounds and the encoding contract are captured exactly.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var settings = new JsonBudgetLedgerSettings(1_024, 2_048, encoding);

        settings.MaximumRecordBytes.ShouldBe(1_024);
        settings.MaximumDocumentBytes.ShouldBe(2_048);
        settings.Encoding.ShouldBeSameAs(encoding);
    }

    /// <summary>Verifies a non-positive record-byte bound is refused and attributed to its exact parameter.</summary>
    [Fact]
    public void Constructor_WhenMaximumRecordBytesIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new JsonBudgetLedgerSettings(0, 1_024, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumRecordBytes");

    /// <summary>Verifies a negative document-byte bound is refused and attributed to its exact parameter.</summary>
    [Fact]
    public void Constructor_WhenMaximumDocumentBytesIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new JsonBudgetLedgerSettings(1_024, -1, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumDocumentBytes");

    /// <summary>Verifies a null encoding contract is refused.</summary>
    [Fact]
    public void Constructor_WhenEncodingIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new JsonBudgetLedgerSettings(1_024, 1_024, null!))
            .ParamName.ShouldBe("encoding");

    /// <summary>Verifies the documented defaults are one-mebibyte bounds with the canonical encoding contract.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_ExposesDocumentedDefaults()
    {
        var settings = JsonBudgetLedgerSettings.CreateDefault();

        settings.MaximumRecordBytes.ShouldBe(1_048_576);
        settings.MaximumDocumentBytes.ShouldBe(1_048_576);
        settings.Encoding.ShouldBe(JsonEncodingSettings.CreateDefault());
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured bound.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var original = JsonBudgetLedgerSettings.CreateDefault();

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.MaximumRecordBytes.ShouldBe(original.MaximumRecordBytes);
        copy.MaximumDocumentBytes.ShouldBe(original.MaximumDocumentBytes);
    }

    /// <summary>Verifies settings with a different record bound are not equal to an otherwise identical instance.</summary>
    [Fact]
    public void Equals_WhenMaximumRecordBytesDiffers_ReportsInequality()
    {
        var encoding = JsonEncodingSettings.CreateDefault();
        var first = new JsonBudgetLedgerSettings(1_024, 2_048, encoding);
        var second = new JsonBudgetLedgerSettings(4_096, 2_048, encoding);

        second.ShouldNotBe(first);
    }
}
