// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteScopeRow behavior and contracts.</summary>
public sealed class SqliteScopeRowTests
{
    /// <summary>Verifies a valid construction exposes exactly its captured evidence.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var (reference, request) = CreateScopeEvidence();
        var parentId = new BudgetScopeId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var row = new SqliteScopeRow(reference, request, parentId, 2);
        row.Reference.ShouldBeSameAs(reference);
        row.Request.ShouldBeSameAs(request);
        row.ParentId.ShouldBe(parentId);
        row.Depth.ShouldBe(2);
    }

    /// <summary>Verifies a nonpositive depth is rejected.</summary>
    [Fact]
    public void Constructor_WhenDepthIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var (reference, request) = CreateScopeEvidence();
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteScopeRow(reference, request, null, 0)).ParamName.ShouldBe("depth");
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var (reference, request) = CreateScopeEvidence();
        var original = new SqliteScopeRow(reference, request, null, 1);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }

    private static (BudgetLedgerScopeReference Reference, BudgetLedgerScopeCreateRequest Request) CreateScopeEvidence()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("20000000-0000-0000-0000-000000000001")), address);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("scope-row-test")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        return (reference, request);
    }
}
