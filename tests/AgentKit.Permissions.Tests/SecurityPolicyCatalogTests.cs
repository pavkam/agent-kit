// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Options;

/// <summary>Verifies <see cref="SecurityPolicyCatalog"/> retention and resolution behavior.</summary>
public sealed class SecurityPolicyCatalogTests
{
    [Fact]
    public async Task ResolveAsync_WhenReferenceIsRetained_ReturnsResolved()
    {
        var snapshot = Snapshot("10000000-0000-0000-0000-000000000001", "sha256:one");
        var options = new AgentPermissionOptions { PolicySnapshot = snapshot };
        var catalog = new SecurityPolicyCatalog(Options.Create(options), []);

        var result = await catalog.ResolveAsync(snapshot, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SecurityPolicySnapshotResolved>().Reference.ShouldBe(snapshot);
    }

    [Fact]
    public async Task ResolveAsync_WhenReferenceIsUnknown_ReturnsStale()
    {
        var options = new AgentPermissionOptions();
        var catalog = new SecurityPolicyCatalog(Options.Create(options), []);
        var missing = Snapshot("10000000-0000-0000-0000-000000000002", "sha256:missing");

        var result = await catalog.ResolveAsync(missing, TestContext.Current.CancellationToken);

        var stale = result.ShouldBeOfType<SecurityPolicySnapshotStale>();
        stale.Reference.ShouldBe(missing);
        stale.SafeReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Constructor_WhenPublicationRepeatsTheSameReference_AcceptsIdempotently()
    {
        var snapshot = Snapshot("10000000-0000-0000-0000-000000000001", "sha256:one");
        var options = new AgentPermissionOptions { PolicySnapshot = snapshot };
        var publication = new SecurityProfilePublication(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000003")),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityProfileKey("profile"),
            new SecurityProfileVersion(1),
            snapshot,
            new ComponentKey<ISecurityAuthority>("authority"));

        _ = new SecurityPolicyCatalog(Options.Create(options), [publication, publication]);
    }

    private static SecurityPolicySnapshotReference Snapshot(string id, string fingerprint) => new(
        new SecurityPolicySnapshotId(Guid.Parse(id)),
        new SecurityPolicyVersion(1),
        new ContentHash(fingerprint));
}
