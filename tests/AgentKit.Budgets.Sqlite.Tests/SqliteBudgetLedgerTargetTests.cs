// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteBudgetLedgerTarget behavior and contracts.</summary>
public sealed class SqliteBudgetLedgerTargetTests
{
    /// <summary>Verifies a nondefault store identity is required.</summary>
    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var path = Path.GetFullPath("target-default-id.db");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteBudgetLedgerTarget(
            path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("expectedStoreInstanceId");
    }

    /// <summary>Verifies combining creation permission with strict validation-only schema handling is rejected.</summary>
    [Fact]
    public void Constructor_WhenCreateIfMissingCombinedWithValidateExact_ThrowsArgumentOutOfRangeException()
    {
        var path = Path.GetFullPath("target-create-validate.db");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteBudgetLedgerTarget(
            path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("schemaMode");
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var path = Path.GetFullPath("target-with-expression.db");
        var original = new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }
}
