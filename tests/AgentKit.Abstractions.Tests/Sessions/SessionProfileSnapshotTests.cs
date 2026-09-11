// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionProfileSnapshot behavior and contracts.</summary>
public sealed class SessionProfileSnapshotTests
{
    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Create(nullReference: true));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("reference");
    }

    [Theory]
    [InlineData("coordinatorKey")]
    [InlineData("runCoordinatorKey")]
    [InlineData("defaultStoreKey")]
    [InlineData("retentionProfile")]
    [InlineData("configurationFingerprint")]
    public void Constructor_WhenSelectedKeyIsUninitialized_ThrowsExactArgumentNullException(string parameterName)
    {
        var exception = Should.Throw<ArgumentNullException>(() => Create(blankParameter: parameterName));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Constructor_WhenCapabilitiesContainUnknownFlag_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(requiredStoreCapabilities: (SessionStoreCapabilities) 16));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("requiredStoreCapabilities");
    }

    [Fact]
    public void Constructor_WhenBusyBehaviorIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(busyBehavior: (SessionBusyBehavior) 2));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("busyBehavior");
    }

    [Theory]
    [InlineData(0, 8, "maximumAppendEntries")]
    [InlineData(-1, 8, "maximumAppendEntries")]
    [InlineData(8, 0, "maximumPageSize")]
    [InlineData(8, -1, "maximumPageSize")]
    public void Constructor_WhenLimitIsNotPositive_ThrowsExactArgumentOutOfRangeException(int maximumAppendEntries, int maximumPageSize, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(maximumAppendEntries: maximumAppendEntries, maximumPageSize: maximumPageSize));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesCompleteImmutableSnapshot()
    {
        var reference = new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(2));
        var snapshot = Create(reference);
        snapshot.Reference.ShouldBe(reference);
        snapshot.CoordinatorKey.ShouldBe(new ComponentKey<ISessionCoordinator>("coordinator"));
        snapshot.RunCoordinatorKey.ShouldBe(new ComponentKey<ISessionRunCoordinator>("run-coordinator"));
        snapshot.DefaultStoreKey.ShouldBe(new SessionStoreKey("store"));
        snapshot.RequiredStoreCapabilities.ShouldBe(SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions);
        snapshot.RequiresDurableStore.ShouldBeTrue();
        snapshot.RequiresDistributedFencing.ShouldBeTrue();
        snapshot.RetentionProfile.ShouldBe(new SessionRetentionProfileKey("retention"));
        snapshot.BusyBehavior.ShouldBe(SessionBusyBehavior.Wait);
        snapshot.MaximumAppendEntries.ShouldBe(8);
        snapshot.MaximumPageSize.ShouldBe(16);
        snapshot.VerifySnapshotHashes.ShouldBeTrue();
        snapshot.DeleteOnDispose.ShouldBeTrue();
        snapshot.ConfigurationFingerprint.ShouldBe(new ContentHash("sha256:profile"));
    }

    private static SessionProfileSnapshot Create(SessionProfileReference? reference = null, bool nullReference = false, string? blankParameter = null, SessionStoreCapabilities requiredStoreCapabilities = SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions, SessionBusyBehavior busyBehavior = SessionBusyBehavior.Wait, int maximumAppendEntries = 8, int maximumPageSize = 16) => new(nullReference ? null! : reference ?? new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)), blankParameter == "coordinatorKey" ? default : new ComponentKey<ISessionCoordinator>("coordinator"), blankParameter == "runCoordinatorKey" ? default : new ComponentKey<ISessionRunCoordinator>("run-coordinator"), blankParameter == "defaultStoreKey" ? default : new SessionStoreKey("store"), requiredStoreCapabilities, requiresDurableStore: true, requiresDistributedFencing: true, blankParameter == "retentionProfile" ? default : new SessionRetentionProfileKey("retention"), busyBehavior, maximumAppendEntries, maximumPageSize, verifySnapshotHashes: true, deleteOnDispose: true, blankParameter == "configurationFingerprint" ? default : new ContentHash("sha256:profile"));
    [Fact]
    public void SessionProfileSnapshot_WhenAppendBoundIsZero_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Profile(maximumAppendEntries: 0));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("maximumAppendEntries");
    }

    private static SessionProfileSnapshot Profile(int maximumAppendEntries = 8) => new(new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)), new ComponentKey<ISessionCoordinator>("coordinator"), new ComponentKey<ISessionRunCoordinator>("run-coordinator"), new SessionStoreKey("store"), SessionStoreCapabilities.None, requiresDurableStore: false, requiresDistributedFencing: false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject, maximumAppendEntries, 16, verifySnapshotHashes: true, deleteOnDispose: false, new ContentHash("sha256:profile"));
}
