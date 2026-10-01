// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies <see cref="OperatingSystemFileDeleter"/> grant binding, audit gating, and no-follow root containment.</summary>
public sealed class OperatingSystemFileDeleterTests
{
    [Fact]
    public async Task DeleteAsync_WhenAuditUnavailable_DeniesAndLeavesTheFile()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "keep.txt"), "data", TestContext.Current.CancellationToken);
        var deleter = CreateDeleter(root, audit: new RejectingAuditDispatcher());

        var result = await deleter.DeleteAsync(CreateAuthorizedDelete(root, "keep.txt"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileDeleteDenied>();
        File.Exists(Path.Combine(root, "keep.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheGrantStoreRefuses_DeniesAndLeavesTheFile()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "keep.txt"), "data", TestContext.Current.CancellationToken);
        var store = new TestSecurity.RecordingGrantStore { Result = new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 0, "refused", intentReceipt: null) };
        var deleter = CreateDeleter(root, grantStore: store);

        var result = await deleter.DeleteAsync(CreateAuthorizedDelete(root, "keep.txt"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileDeleteDenied>();
        File.Exists(Path.Combine(root, "keep.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorized_PresentsADeleteEffectOnTheExactResourceWithTheDeleteFingerprint()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "data", TestContext.Current.CancellationToken);
        var store = new TestSecurity.RecordingGrantStore();
        var deleter = CreateDeleter(root, grantStore: store);
        var operation = CreateAuthorizedDelete(root, "gone.txt");

        var result = await deleter.DeleteAsync(operation, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<FileDeleteSuccess>().PreviousBytes.ShouldBe(4);
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Delete);
        enforcement.Resources.ShouldBe([FileSecurityBinding.Resource(operation.ResolvedTarget)]);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.DeleteFingerprint(operation));
        File.Exists(Path.Combine(root, "gone.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheTargetIsASymbolicLink_DeniesAndLeavesBothTheLinkAndItsDestination()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        var destination = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(destination, "secret", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(root, "link.txt"), destination);
        var deleter = CreateDeleter(root);

        var result = await deleter.DeleteAsync(CreateAuthorizedDelete(root, "link.txt"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileDeleteDenied>();
        File.Exists(destination).ShouldBeTrue();
        _ = new FileInfo(Path.Combine(root, "link.txt")).LinkTarget.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenAParentSegmentIsASymbolicLink_DeniesAndLeavesTheOutsideFile()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        var destination = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(destination, "secret", TestContext.Current.CancellationToken);
        _ = Directory.CreateSymbolicLink(Path.Combine(root, "dir"), outside);
        var deleter = CreateDeleter(root);

        var result = await deleter.DeleteAsync(CreateAuthorizedDelete(root, "dir/secret.txt"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileDeleteDenied>();
        File.Exists(destination).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheResolvedTargetLeavesTheConfiguredRoot_DeniesBeforeAnyEffect()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        var destination = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(destination, "secret", TestContext.Current.CancellationToken);
        var store = new TestSecurity.RecordingGrantStore();
        var deleter = CreateDeleter(root, grantStore: store);
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"), new NormalizedRelativePath("secret.txt"), destination, FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8), FileSecurityBinding.ContentFingerprint("target"u8));

        var result = await deleter.DeleteAsync(new AuthorizedFileDelete(target, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileDeleteDenied>();
        store.LastEnforcement.ShouldBeNull();
        File.Exists(destination).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheTargetIsAnEmptyDirectory_ConflictsAndKeepsTheDirectory()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        _ = Directory.CreateDirectory(Path.Combine(root, "empty"));
        var deleter = CreateDeleter(root);

        var result = await deleter.DeleteAsync(CreateAuthorizedDelete(root, "empty"), TestContext.Current.CancellationToken);

        result.ShouldNotBeOfType<FileDeleteSuccess>();
        Directory.Exists(Path.Combine(root, "empty")).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenCancelledBeforeTheEffect_ThrowsAndLeavesTheFile()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "keep.txt"), "data", TestContext.Current.CancellationToken);
        var deleter = CreateDeleter(root);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await deleter.DeleteAsync(CreateAuthorizedDelete(root, "keep.txt"), cancelled.Token));

        File.Exists(Path.Combine(root, "keep.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheOperationIsNull_ThrowsNamingIt()
    {
        var deleter = CreateDeleter(CreateTempRoot());

        (await Should.ThrowAsync<ArgumentNullException>(async () => await deleter.DeleteAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("operation");
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"agentkit-fs-delete-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static IFileDeleter CreateDeleter(
        string root,
        ISecurityAuditDispatcher? audit = null,
        ISecurityGrantStore? grantStore = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(grantStore ?? TestSecurity.GrantStore());
        _ = services.AddSingleton(audit ?? new AcceptingAuditDispatcher());
        _ = services.AddOperatingSystemFileSystem(
            new FileSystemProfileKey("test"),
            options => options.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root)));
        return services.BuildServiceProvider().GetRequiredKeyedService<IFileDeleter>("test");
    }

    private static AuthorizedFileDelete CreateAuthorizedDelete(string root, string relativePath, ContentHash? expectedTargetFingerprint = null)
    {
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            Path.GetFullPath(Path.Combine(root, relativePath)),
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            expectedTargetFingerprint ?? FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedFileDelete(target, expectedTargetFingerprint, TestSecurity.Grant());
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private sealed class RejectingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditUnavailable("No sink."));
    }
}
