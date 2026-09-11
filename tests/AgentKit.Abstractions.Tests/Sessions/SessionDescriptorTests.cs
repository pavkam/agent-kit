// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionDescriptor behavior and contracts.</summary>
public sealed class SessionDescriptorTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    [Fact]
    public void SessionDescriptor_Constructor_WhenAddressNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDescriptor(null!, null, new TenantId("t"), new PrincipalId("p"), new SessionStoreKey("store"), BranchId, new SessionVersion(0), SessionLifecycleState.Active, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), ExtensionData.Empty));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void SessionDescriptor_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDescriptor(Address(), null, new TenantId("t"), new PrincipalId("p"), new SessionStoreKey("store"), BranchId, new SessionVersion(0), SessionLifecycleState.Active, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void SessionDescriptor_Equality_WhenSameValues_InstancesAreEqual() => Descriptor().ShouldBe(Descriptor());
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static SessionAddress Address() => new(AgentId, SessionId);
    private static SessionDescriptor Descriptor() => new(Address(), null, new TenantId("t"), new PrincipalId("p"), new SessionStoreKey("store"), BranchId, new SessionVersion(0), SessionLifecycleState.Active, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), ExtensionData.Empty);
}
