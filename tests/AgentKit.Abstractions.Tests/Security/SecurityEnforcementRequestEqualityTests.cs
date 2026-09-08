// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies complete structural equality for durable concrete enforcement evidence.</summary>
public sealed class SecurityEnforcementRequestEqualityTests
{
    /// <summary>Verifies separately reconstructed ordered resources compare and hash identically.</summary>
    [Fact]
    public void Equals_WhenEvidenceIsSeparatelyReconstructed_ReturnsTrueWithEqualHash()
    {
        var first = Create();
        var second = Create() with
        {
            Resources = [
                new ProtectedResource(ProtectedResourceKind.File, "first"),
                new ProtectedResource(ProtectedResourceKind.File, "second"),
            ],
        };

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Equals(null).ShouldBeFalse();
    }

    /// <summary>Verifies every scalar, context, and ordered-resource difference changes equality.</summary>
    [Fact]
    public void Equals_WhenAnyEvidenceDiffers_ReturnsFalse()
    {
        var value = Create();
        var otherIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"),
            new PrincipalId("other"), ExecutionSubjectKind.Service);
        var changes = new SecurityEnforcementRequest[]
        {
            value with { Scope = CreateScope(2) },
            value with { Identity = otherIdentity },
            Create(captured: true),
            value with { Audience = new ComponentId("other") },
            value with { Kind = SecurityOperationKind.FileWrite },
            value with { Effect = SecurityEffect.Mutate },
            value with { Resources = [value.Resources[1], value.Resources[0]] },
            value with { InputFingerprint = new InputFingerprint("sha256:changed") },
            value with { RevocationVersion = new SecurityRevocationVersion(2) },
        };

        changes.ShouldAllBe(candidate => candidate != value);
    }

    /// <summary>Verifies malformed default resource copies remain total values for equality and hashing.</summary>
    [Fact]
    public void Equals_WhenResourcesAreDefault_RemainsTotalAndDistinguishesValidEvidence()
    {
        var malformed = Create() with { Resources = default };
        var reconstructed = Create() with { Resources = default };

        malformed.ShouldBe(reconstructed);
        malformed.GetHashCode().ShouldBe(reconstructed.GetHashCode());
        malformed.ShouldNotBe(Create());
    }

    private static SecurityEnforcementRequest Create(bool captured = false)
    {
        var scope = CreateScope(1);
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"),
            new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var resources = ImmutableArray.Create(
            new ProtectedResource(ProtectedResourceKind.File, "first"),
            new ProtectedResource(ProtectedResourceKind.File, "second"));
        if (!captured)
        {
            return new SecurityEnforcementRequest(scope, identity, new ComponentId("filesystem"),
                SecurityOperationKind.FileRead, SecurityEffect.Observe, resources,
                new InputFingerprint("sha256:abc"), new SecurityRevocationVersion(1));
        }
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("profile"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:snapshot")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            scope,
            identity);
        return new SecurityEnforcementRequest(scope, identity, authorization,
            new ComponentId("filesystem"), SecurityOperationKind.FileRead, SecurityEffect.Observe,
            resources, new InputFingerprint("sha256:abc"), new SecurityRevocationVersion(1));
    }

    private static SecurityAuthorizationScope CreateScope(int discriminator) => new(
        new AgentId(Guid.Parse($"10000000-0000-0000-0000-{discriminator:D12}")),
        null,
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse($"20000000-0000-0000-0000-{discriminator:D12}")), null));
}
