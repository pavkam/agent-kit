// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared decision-store contract against the durable JSON adapter.</summary>
public sealed class JsonSecurityDecisionStoreTests: SecurityDecisionStoreConformanceTests<JsonSecurityDecisionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonSecurityDecisionStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies decisions survive process-level store recreation against the same root.</summary>
    [Fact]
    public async Task RecordAsync_WhenStoreIsDisposedAndReopened_RetainsDecisions()
    {
        await using var fixture = new JsonSecurityDecisionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var decision = new SecurityDenied(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(2),
            new SecurityDenial("security.test", "Denied."));
        await store.RecordAsync(decision, TestContext.Current.CancellationToken);

        var reopened = await fixture.RecreateAsync(TestContext.Current.CancellationToken);
        fixture.ReadRecorded(reopened).ShouldBe([decision]);
    }
}
