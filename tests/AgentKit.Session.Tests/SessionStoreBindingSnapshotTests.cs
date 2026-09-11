// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies SessionStoreBindingSnapshot behavior and contracts.</summary>
public sealed class SessionStoreBindingSnapshotTests
{
    [Fact]
    public async Task SharedSnapshot_WhenCatalogAndSelectorResolve_ReadsEachDescriptorOnce()
    {
        var store = new FakeSessionStore();
        var snapshot = new SessionStoreBindingSnapshot([store]);
        var catalog = new DefaultSessionStoreCatalog(snapshot);
        var selector = new DefaultSessionStoreSelector(snapshot, NullLogger<DefaultSessionStoreSelector>.Instance);
        var result = await selector.SelectForCreateAsync(new SessionStoreCreateSelectionRequest(TestFactory.CreateRequest(), Profile("fake")), TestContext.Current.CancellationToken);
        catalog.GetDescriptors().Length.ShouldBe(1);
        _ = result.ShouldBeOfType<SessionStoreSelected>();
        store.DescriptorReadCount.ShouldBe(1);
    }

    private static SessionProfileSnapshot Profile(string storeKey, bool requiresDurableStore = false) => new(new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)), new ComponentKey<ISessionCoordinator>("coordinator"), new ComponentKey<ISessionRunCoordinator>("run-coordinator"), new SessionStoreKey(storeKey), SessionStoreCapabilities.None, requiresDurableStore, requiresDistributedFencing: false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject, maximumAppendEntries: 8, maximumPageSize: 16, verifySnapshotHashes: true, deleteOnDispose: false, new ContentHash("sha256:profile"));
}
