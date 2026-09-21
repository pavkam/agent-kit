// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared decision-store contract against the durable SQLite adapter.</summary>
public sealed class SqliteSecurityDecisionStoreTests: SecurityDecisionStoreConformanceTests<SqliteSecurityDecisionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteSecurityDecisionStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies decisions survive process-level store recreation against the same database.</summary>
    [Fact]
    public async Task RecordAsync_WhenStoreIsDisposedAndReopened_RetainsDecisions()
    {
        await using var fixture = new SqliteSecurityDecisionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var decision = new SecurityDenied(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000002")),
            new SecurityPolicyVersion(3),
            new SecurityDenial("security.test", "Denied."));
        await store.RecordAsync(decision, TestContext.Current.CancellationToken);

        var reopened = await fixture.RecreateAsync(TestContext.Current.CancellationToken);
        fixture.ReadRecorded(reopened).ShouldBe([decision]);
    }
}
