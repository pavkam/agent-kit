// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

/// <summary>Verifies <see cref="InMemorySecurityDecisionStore"/> behavior and contracts.</summary>
public sealed class InMemorySecurityDecisionStoreTests
{
    [Fact]
    public async Task RecordAsync_WhenDecisionIsNull_RejectsExactArgument()
    {
        var store = new InMemorySecurityDecisionStore();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await store.RecordAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("decision");
    }

    [Fact]
    public async Task RecordAsync_WhenCalled_AppendsInOrder()
    {
        var store = new InMemorySecurityDecisionStore();
        var first = Denied("security.no_policy");
        var second = Denied("security.approval_denied");

        await store.RecordAsync(first, TestContext.Current.CancellationToken);
        await store.RecordAsync(second, TestContext.Current.CancellationToken);

        store.Decisions.Count.ShouldBe(2);
        store.Decisions[0].ShouldBeSameAs(first);
        store.Decisions[1].ShouldBeSameAs(second);
    }

    private static SecurityDenied Denied(string code) =>
        new(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new SecurityPolicyVersion(1),
            new SecurityDenial(code, "Denied."));
}
