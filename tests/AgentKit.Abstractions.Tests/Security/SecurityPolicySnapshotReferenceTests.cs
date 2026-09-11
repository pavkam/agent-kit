// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityPolicySnapshotReference behavior and contracts.</summary>
public sealed class SecurityPolicySnapshotReferenceTests
{
    /// <summary>Verifies snapshot references reject each default nested value with its owning parameter name.</summary>
    [Fact]
    public void Constructor_WhenSnapshotReferencePartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotReference(default, PolicyVersion(), Hash())).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotReference(SnapshotId(), default, Hash())).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => new SecurityPolicySnapshotReference(SnapshotId(), PolicyVersion(), default)).ParamName.ShouldBe("fingerprint");
    }

    private static SecurityPolicySnapshotId SnapshotId() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static SecurityPolicyVersion PolicyVersion() => new(3);
    private static ContentHash Hash() => new("sha256:test");
    /// <summary>Verifies snapshot aggregates have structural equality and get-only public state.</summary>
    [Fact]
    public void Equality_WhenCapturedValuesMatch_IsStructuralAndImmutable()
    {
        Reference().ShouldBe(Reference());
        typeof(SecurityPolicySnapshotReference).GetProperties().ShouldAllBe(property => property.SetMethod == null);
    }

    private static SecurityPolicySnapshotReference Reference() => new(SnapshotId(), PolicyVersion(), Hash());
}
