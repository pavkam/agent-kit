// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs approval-store conformance against explicitly ephemeral process-local storage.</summary>
public sealed class InMemoryApprovalStoreTests: ApprovalStoreConformanceTests<InMemoryApprovalStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemoryApprovalStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies the adapter never claims process-loss durability.</summary>
    [Fact]
    public async Task Capabilities_WhenRead_ReportsEphemeralStorage()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        store.Capabilities.IsDurable.ShouldBeFalse();
    }
}
