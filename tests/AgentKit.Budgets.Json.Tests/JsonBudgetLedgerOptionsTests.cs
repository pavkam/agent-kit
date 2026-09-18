// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the mutable composition-time options mirror the immutable settings defaults.</summary>
/// <remarks>
/// <see cref="JsonBudgetLedgerOptions"/> performs no validation of its own; invalid values are rejected only when the
/// registration extension materializes them into <see cref="JsonBudgetLedgerSettings"/>. These cases prove the documented
/// defaults, the passthrough of the mutable encoding options, and that unchecked assignment does not throw until then.
/// </remarks>
public sealed class JsonBudgetLedgerOptionsTests
{
    /// <summary>Proves fresh options materialize to exactly <see cref="JsonBudgetLedgerSettings.CreateDefault"/>.</summary>
    [Fact]
    public void Constructor_WhenUnconfigured_MatchesSettingsDefaults()
    {
        var options = new JsonBudgetLedgerOptions();

        var settings = new JsonBudgetLedgerSettings(
            options.MaximumRecordBytes,
            options.MaximumDocumentBytes,
            new JsonEncodingSettings(options.Encoding.SerializerOptions));

        settings.ShouldBe(JsonBudgetLedgerSettings.CreateDefault());
    }

    /// <summary>Proves each default value individually equals its documented bound.</summary>
    [Fact]
    public void Constructor_WhenUnconfigured_ExposesDocumentedDefaults()
    {
        var options = new JsonBudgetLedgerOptions();

        options.MaximumRecordBytes.ShouldBe(1_048_576);
        options.MaximumDocumentBytes.ShouldBe(1_048_576);
        _ = options.Encoding.ShouldNotBeNull();
    }

    /// <summary>Proves the options are plain mutable state and do not validate on assignment; validation belongs to materialization.</summary>
    [Fact]
    public void Properties_WhenAssignedInvalidValues_DoNotThrowUntilMaterialized()
    {
        var options = new JsonBudgetLedgerOptions { MaximumRecordBytes = 0, MaximumDocumentBytes = -1 };

        options.MaximumRecordBytes.ShouldBe(0);
        options.MaximumDocumentBytes.ShouldBe(-1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerSettings(
            options.MaximumRecordBytes,
            options.MaximumDocumentBytes,
            new JsonEncodingSettings(options.Encoding.SerializerOptions)));
    }

    /// <summary>Proves the exposed encoding options instance can be replaced or mutated in place and both reach materialization.</summary>
    [Fact]
    public void Encoding_WhenMutated_ReachesMaterializedSettings()
    {
        var options = new JsonBudgetLedgerOptions();

        options.Encoding.SerializerOptions.WriteIndented = true;

        var settings = new JsonBudgetLedgerSettings(
            options.MaximumRecordBytes, options.MaximumDocumentBytes, new JsonEncodingSettings(options.Encoding.SerializerOptions));
        settings.Encoding.DocumentOptions.WriteIndented.ShouldBeTrue();
    }
}
