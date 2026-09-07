// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

public sealed partial class SandboxedFileSystem
{
    private const uint _linuxRenameNoReplace = 1;
    private const uint _macOsRenameExclusive = 0x00000004;

    /// <inheritdoc/>
    private async ValueTask<WorkspacePatchResult> ApplyPatchCoreAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfDefaultOrEmpty(request.Entries);
        ArgumentException.ThrowIfContainsNull(request.Entries);
        foreach (var entry in request.Entries)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(entry.Id.Value, Guid.Empty, nameof(entry.Id));
            ArgumentOutOfRangeException.ThrowIfUndefined(entry.Kind);
            ArgumentNullException.ThrowIfNull(entry.Grant);
            if (entry is WorkspacePatchCreate create)
            {
                ArgumentException.ThrowIfDefault(create.Content);
            }
            else if (entry is WorkspacePatchReplace replace)
            {
                ArgumentException.ThrowIfDefault(replace.Content);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var structuralFailure = ValidatePatchStructure(request);
        if (structuralFailure is not null)
        {
            return RejectPatch(request, structuralFailure);
        }

        if (!IsSecureTraversalSupported)
        {
            return RejectPatch(request, "Secure no-follow patch traversal is unavailable on this platform.");
        }

        for (var index = 0; index < request.Entries.Length; index++)
        {
            var entry = request.Entries[index];
            var grantFailure = await ValidatePatchGrantAsync(entry, cancellationToken).ConfigureAwait(false);
            if (grantFailure is not null)
            {
                return RejectPatch(request, $"Entry {index} was denied: {grantFailure}");
            }
        }

        var lockPaths = PatchPaths(request)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var leases = new List<MutationLockLease>(lockPaths.Length);
        var prepared = new List<PreparedPatchEntry>(request.Entries.Length);
        try
        {
            foreach (var path in lockPaths)
            {
                leases.Add(await AcquireMutationLockAsync(path, cancellationToken).ConfigureAwait(false));
            }

            for (var index = 0; index < request.Entries.Length; index++)
            {
                var (item, failure) = await PreparePatchEntryAsync(
                    index, request.Entries[index], cancellationToken).ConfigureAwait(false);
                if (item is null)
                {
                    return RejectPatch(request, $"Entry {index} failed preflight: {failure}");
                }

                prepared.Add(item);
            }

            foreach (var item in prepared)
            {
                var stagingFailure = await StagePatchEntryAsync(item, cancellationToken).ConfigureAwait(false);
                if (stagingFailure is not null)
                {
                    return RejectPatch(request, $"Entry {item.Index} could not be staged: {stagingFailure}");
                }
            }

            foreach (var item in prepared)
            {
                var revalidationFailure = await RevalidatePatchEntryAsync(item, cancellationToken).ConfigureAwait(false);
                if (revalidationFailure is not null)
                {
                    return RejectPatch(request, $"Entry {item.Index} changed before commit: {revalidationFailure}");
                }
            }

            var committed = 0;
            var durabilityConfirmed = true;
            try
            {
                foreach (var item in prepared)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var revalidationFailure = await RevalidatePatchEntryAsync(item, cancellationToken).ConfigureAwait(false);
                    if (revalidationFailure is not null)
                    {
                        return SettlePatch(
                            request,
                            committed,
                            $"Entry {item.Index} changed during commit: {revalidationFailure}");
                    }

                    var commitFailure = CommitPatchEntry(item);
                    if (commitFailure is not null)
                    {
                        return SettlePatch(request, committed, $"Entry {item.Index} failed to commit: {commitFailure}");
                    }

                    item.StagingExists = false;
                    committed++;
                    durabilityConfirmed &= Synchronize(item.Parent.DangerousGetHandle().ToInt32()) == 0;
                    if (item.DestinationParent is not null &&
                        !ReferenceEquals(item.Parent, item.DestinationParent))
                    {
                        durabilityConfirmed &= Synchronize(
                            item.DestinationParent.DangerousGetHandle().ToInt32()) == 0;
                    }
                }
            }
            catch (OperationCanceledException) when (committed > 0)
            {
                return SettlePatch(request, committed, "Cancellation arrived after a committed patch prefix.");
            }

            return SettlePatch(
                request,
                committed,
                durabilityConfirmed ? null : "The patch committed, but directory durability could not be confirmed.");
        }
        finally
        {
            foreach (var item in prepared)
            {
                item.Dispose();
            }

            for (var index = leases.Count - 1; index >= 0; index--)
            {
                leases[index].Dispose();
            }
        }
    }

    private string? ValidatePatchStructure(WorkspacePatchRequest request)
    {
        if (request.Entries.Length > _maximumPatchEntries)
        {
            return "The patch exceeds the configured entry boundary.";
        }

        long totalBytes = 0;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<WorkspaceMutationId>();
        foreach (var entry in request.Entries)
        {
            var expectedKind = entry switch
            {
                WorkspacePatchCreate => WorkspacePatchEntryKind.Create,
                WorkspacePatchReplace => WorkspacePatchEntryKind.Replace,
                WorkspacePatchDelete => WorkspacePatchEntryKind.Delete,
                WorkspacePatchMove => WorkspacePatchEntryKind.Move,
                _ => throw new UnreachableException(),
            };
            if (entry.Kind != expectedKind)
            {
                return "A patch entry kind does not match its concrete contract.";
            }

            if (!identities.Add(entry.Id))
            {
                return "Patch mutation identities must be unique.";
            }

            foreach (var path in PatchPaths(entry))
            {
                if (!paths.Add(path))
                {
                    return "A patch cannot touch the same path more than once.";
                }
            }

            var contentLength = entry switch
            {
                WorkspacePatchCreate create => create.Content.Length,
                WorkspacePatchReplace replace => replace.Content.Length,
                _ => 0,
            };
            if (contentLength > _maximumWriteBytes)
            {
                return "A patch entry exceeds the configured write boundary.";
            }

            if (totalBytes > _maximumPatchBytes - contentLength)
            {
                return "The patch exceeds the configured aggregate byte boundary.";
            }

            totalBytes += contentLength;
        }

        return null;
    }

    private async ValueTask<string?> ValidatePatchGrantAsync(
        WorkspacePatchEntry entry,
        CancellationToken cancellationToken)
    {
        var (effect, resources, fingerprint) = entry switch
        {
            WorkspacePatchCreate create => (
                SecurityEffect.Create,
                WorkspacePatchSecurityBinding.CreateResources(create.Id, create.Path),
                WorkspacePatchSecurityBinding.CreateFingerprint(create.Id, create.Path, create.Content)),
            WorkspacePatchReplace replace => (
                SecurityEffect.Replace,
                FileSecurityBinding.AtomicReplaceResources(replace.Id, replace.Path),
                FileSecurityBinding.AtomicReplaceFingerprint(
                    replace.Id,
                    replace.Path,
                    replace.ExpectedContentFingerprint,
                    replace.Content)),
            WorkspacePatchDelete delete => (
                SecurityEffect.Delete,
                WorkspacePatchSecurityBinding.DeleteResources(delete.Path),
                WorkspacePatchSecurityBinding.DeleteFingerprint(delete.Path, delete.ExpectedContentFingerprint)),
            WorkspacePatchMove move => (
                SecurityEffect.Move,
                WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath),
                WorkspacePatchSecurityBinding.MoveFingerprint(
                    move.SourcePath,
                    move.DestinationPath,
                    move.ExpectedContentFingerprint)),
            _ => throw new UnreachableException(),
        };

        var result = await _grantStore.ValidateAndConsumeAsync(
            entry.Grant,
            new SecurityEnforcementRequest(
                entry.Grant.Scope,
                entry.Grant.Identity,
                SecurityAudience,
                SecurityOperationKind.FileWrite,
                effect,
                resources,
                fingerprint,
                entry.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        return result.Status == GrantConsumptionStatus.Consumed ? null : result.SafeMessage;
    }

    private async ValueTask<(PreparedPatchEntry? Item, string? Failure)> PreparePatchEntryAsync(
        int index,
        WorkspacePatchEntry entry,
        CancellationToken cancellationToken)
    {
        var sourcePath = PatchSourcePath(entry);
        if (!TryOpenParentDirectory(
                sourcePath,
                createMissingDirectories: false,
                cancellationToken,
                out var parent,
                out var name,
                out var traversalError))
        {
            return (null, PatchTraversalFailure(traversalError));
        }

        SafeFileHandle? destinationParent = null;
        try
        {
            var item = new PreparedPatchEntry(index, entry, parent, name);
            switch (entry)
            {
                case WorkspacePatchCreate:
                    if (!TargetIsAbsent(parent, name, out var absentFailure))
                    {
                        item.Dispose();
                        return (null, absentFailure);
                    }

                    break;
                case WorkspacePatchReplace replace:
                    var (replaceSnapshot, replaceMode, replaceFailure) = await ObservePatchFileAsync(
                        parent, name, cancellationToken).ConfigureAwait(false);
                    if (replaceSnapshot?.ContentFingerprint != replace.ExpectedContentFingerprint)
                    {
                        item.Dispose();
                        return (null, replaceFailure ?? "The replacement target no longer matches its planned version.");
                    }

                    item.Mode = replaceMode;
                    break;
                case WorkspacePatchDelete delete:
                    var (deleteSnapshot, _, deleteFailure) = await ObservePatchFileAsync(
                        parent, name, cancellationToken).ConfigureAwait(false);
                    if (deleteSnapshot?.ContentFingerprint != delete.ExpectedContentFingerprint)
                    {
                        item.Dispose();
                        return (null, deleteFailure ?? "The delete target no longer matches its planned version.");
                    }

                    break;
                case WorkspacePatchMove move:
                    var (moveSnapshot, _, moveFailure) = await ObservePatchFileAsync(
                        parent, name, cancellationToken).ConfigureAwait(false);
                    if (moveSnapshot?.ContentFingerprint != move.ExpectedContentFingerprint)
                    {
                        item.Dispose();
                        return (null, moveFailure ?? "The move source no longer matches its planned version.");
                    }

                    if (!TryOpenParentDirectory(
                            move.DestinationPath,
                            createMissingDirectories: false,
                            cancellationToken,
                            out destinationParent,
                            out var destinationName,
                            out traversalError))
                    {
                        item.Dispose();
                        return (null, PatchTraversalFailure(traversalError));
                    }

                    item.DestinationParent = destinationParent;
                    item.DestinationName = destinationName;
                    destinationParent = null;
                    if (!TargetIsAbsent(item.DestinationParent, destinationName, out absentFailure))
                    {
                        item.Dispose();
                        return (null, absentFailure);
                    }

                    break;
                default:
                    throw new UnreachableException();
            }

            return (item, null);
        }
        catch
        {
            parent.Dispose();
            throw;
        }
        finally
        {
            destinationParent?.Dispose();
        }
    }

    private static async ValueTask<string?> StagePatchEntryAsync(
        PreparedPatchEntry item,
        CancellationToken cancellationToken)
    {
        var content = item.Entry switch
        {
            WorkspacePatchCreate create => create.Content,
            WorkspacePatchReplace replace => replace.Content,
            _ => default,
        };
        if (content.IsDefault)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stagingPath = FileSecurityBinding.AtomicReplaceStagingPath(item.Entry.Id, PatchSourcePath(item.Entry));
        item.StagingName = FileName(stagingPath);
        var descriptor = OpenAt(
            item.Parent.DangerousGetHandle().ToInt32(),
            item.StagingName,
            _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
            _ownerReadWritePermissions);
        if (descriptor < 0)
        {
            return "The private staging file could not be created.";
        }

        item.StagingExists = true;
        try
        {
            using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            await using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false);
            var mode = item.Entry is WorkspacePatchReplace ? item.Mode & 0x0FFF : _ownerReadWritePermissions;
            if (ChangeMode(descriptor, mode) < 0)
            {
                return "The staged file mode could not be established.";
            }

            await stream.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "The exact patch bytes could not be staged.";
        }
    }

    private async ValueTask<string?> RevalidatePatchEntryAsync(
        PreparedPatchEntry item,
        CancellationToken cancellationToken)
    {
        switch (item.Entry)
        {
            case WorkspacePatchCreate:
                return TargetIsAbsent(item.Parent, item.Name, out var createFailure) ? null : createFailure;
            case WorkspacePatchReplace replace:
                var (replaceSnapshot, _, replaceFailure) = await ObservePatchFileAsync(
                    item.Parent, item.Name, cancellationToken).ConfigureAwait(false);
                return replaceSnapshot?.ContentFingerprint == replace.ExpectedContentFingerprint
                    ? null
                    : replaceFailure ?? "The replacement target version changed.";
            case WorkspacePatchDelete delete:
                var (deleteSnapshot, _, deleteFailure) = await ObservePatchFileAsync(
                    item.Parent, item.Name, cancellationToken).ConfigureAwait(false);
                return deleteSnapshot?.ContentFingerprint == delete.ExpectedContentFingerprint
                    ? null
                    : deleteFailure ?? "The delete target version changed.";
            case WorkspacePatchMove move:
                var (moveSnapshot, _, moveFailure) = await ObservePatchFileAsync(
                    item.Parent, item.Name, cancellationToken).ConfigureAwait(false);
                if (moveSnapshot?.ContentFingerprint != move.ExpectedContentFingerprint)
                {
                    return moveFailure ?? "The move source version changed.";
                }

                return TargetIsAbsent(item.DestinationParent!, item.DestinationName!, out var moveDestinationFailure)
                    ? null
                    : moveDestinationFailure;
            default:
                throw new UnreachableException();
        }
    }

    private static string? CommitPatchEntry(PreparedPatchEntry item)
    {
        var parentDescriptor = item.Parent.DangerousGetHandle().ToInt32();
        int result;
        try
        {
            result = item.Entry switch
            {
                WorkspacePatchCreate => RenameNoReplace(
                    parentDescriptor, item.StagingName!, parentDescriptor, item.Name),
                WorkspacePatchReplace => RenameAt(
                    parentDescriptor, item.StagingName!, parentDescriptor, item.Name),
                WorkspacePatchDelete => UnlinkAt(parentDescriptor, item.Name, 0),
                WorkspacePatchMove => RenameNoReplace(
                    parentDescriptor,
                    item.Name,
                    item.DestinationParent!.DangerousGetHandle().ToInt32(),
                    item.DestinationName!),
                _ => throw new UnreachableException(),
            };
        }
        catch (EntryPointNotFoundException)
        {
            return "The host does not provide the required no-replace rename primitive.";
        }

        return result == 0 ? null : "The host refused the exact target-state transition.";
    }

    private async ValueTask<(FileSnapshotResult? Snapshot, int Mode, string? Failure)> ObservePatchFileAsync(
        SafeFileHandle parent,
        string name,
        CancellationToken cancellationToken)
    {
        var descriptor = OpenAt(
            parent.DangerousGetHandle().ToInt32(),
            name,
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            return (null, 0, Marshal.GetLastPInvokeError() == _errorNotFound
                ? "The required file does not exist."
                : "The required file could not be opened safely.");
        }

        using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        if (!TryGetFileMode(descriptor, out var mode))
        {
            return (null, 0, "The required file mode could not be observed.");
        }

        var snapshot = await ReadSnapshotFromHandleAsync(
            handle, _maximumReadBytes, cancellationToken).ConfigureAwait(false);
        return snapshot.Status == FileSnapshotStatus.Success
            ? (snapshot, mode, null)
            : (null, mode, snapshot.SafeMessage ?? "The required file could not be verified.");
    }

    private static bool TargetIsAbsent(SafeFileHandle parent, string name, out string? failure)
    {
        var descriptor = OpenAt(
            parent.DangerousGetHandle().ToInt32(),
            name,
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (descriptor >= 0)
        {
            using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            failure = "The target already exists.";
            return false;
        }

        var error = Marshal.GetLastPInvokeError();
        failure = error == _errorNotFound ? null : "Target absence could not be verified safely.";
        return error == _errorNotFound;
    }

    private static WorkspacePatchResult RejectPatch(WorkspacePatchRequest request, string message) =>
        new(WorkspacePatchStatus.RejectedBeforeEffect, PatchResults(request, 0), message);

    private static WorkspacePatchResult SettlePatch(
        WorkspacePatchRequest request,
        int committed,
        string? message)
    {
        var status = committed == request.Entries.Length
            ? committed == 1
                ? WorkspacePatchStatus.AtomicCommitted
                : WorkspacePatchStatus.CommittedWithNonAtomicVisibility
            : committed == 0
                ? WorkspacePatchStatus.RejectedBeforeEffect
                : WorkspacePatchStatus.Partial;
        return new WorkspacePatchResult(status, PatchResults(request, committed), message);
    }

    private static ImmutableArray<WorkspacePatchEntryResult> PatchResults(
        WorkspacePatchRequest request,
        int committed)
    {
        var results = ImmutableArray.CreateBuilder<WorkspacePatchEntryResult>(request.Entries.Length);
        for (var index = 0; index < request.Entries.Length; index++)
        {
            var entry = request.Entries[index];
            results.Add(new WorkspacePatchEntryResult(
                index,
                entry.Kind,
                index < committed ? WorkspacePatchEntryStatus.Committed : WorkspacePatchEntryStatus.Unchanged,
                PatchSourcePath(entry),
                entry is WorkspacePatchMove move ? move.DestinationPath : null,
                index < committed ? PatchCommittedFingerprint(entry) : null,
                null));
        }

        return results.MoveToImmutable();
    }

    private static ContentHash? PatchCommittedFingerprint(WorkspacePatchEntry entry) => entry switch
    {
        WorkspacePatchCreate create => FileSecurityBinding.ContentFingerprint(create.Content.AsSpan()),
        WorkspacePatchReplace replace => FileSecurityBinding.ContentFingerprint(replace.Content.AsSpan()),
        WorkspacePatchMove move => move.ExpectedContentFingerprint,
        _ => null,
    };

    private static FileSystemPath PatchSourcePath(WorkspacePatchEntry entry) => entry switch
    {
        WorkspacePatchCreate create => create.Path,
        WorkspacePatchReplace replace => replace.Path,
        WorkspacePatchDelete delete => delete.Path,
        WorkspacePatchMove move => move.SourcePath,
        _ => throw new UnreachableException(),
    };

    private static IEnumerable<string> PatchPaths(WorkspacePatchRequest request) =>
        request.Entries.SelectMany(PatchPaths);

    private static IEnumerable<string> PatchPaths(WorkspacePatchEntry entry)
    {
        yield return PatchSourcePath(entry).Value;
        if (entry is WorkspacePatchMove move)
        {
            yield return move.DestinationPath.Value;
        }
    }

    private static string PatchTraversalFailure(int error) => error == _errorNotFound
        ? "A required parent directory does not exist."
        : IsBoundaryViolation(error)
            ? "The path crosses an inaccessible boundary."
            : "The path could not be resolved safely.";

    private static string FileName(FileSystemPath path)
    {
        var separator = path.Value.LastIndexOf('/');
        return separator < 0 ? path.Value : path.Value[(separator + 1)..];
    }

    private static int RenameNoReplace(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath)
    {
        if (OperatingSystem.IsLinux())
        {
            return RenameAt2(
                oldDirectoryDescriptor,
                oldPath,
                newDirectoryDescriptor,
                newPath,
                _linuxRenameNoReplace);
        }

        if (OperatingSystem.IsMacOS())
        {
            return RenameAtExclusive(
                oldDirectoryDescriptor,
                oldPath,
                newDirectoryDescriptor,
                newPath,
                _macOsRenameExclusive);
        }

        Marshal.SetLastPInvokeError(_errorInvalidArgument);
        return -1;
    }

    /// <summary>Holds opened parent descriptors and staging state for one preflighted entry.</summary>
    private sealed class PreparedPatchEntry: IDisposable
    {
        /// <summary>Initializes one prepared entry and assumes ownership of <paramref name="parent"/>.</summary>
        public PreparedPatchEntry(int index, WorkspacePatchEntry entry, SafeFileHandle parent, string name)
        {
            Index = index;
            Entry = entry;
            Parent = parent;
            Name = name;
        }

        /// <summary>Gets the source ordinal.</summary>
        public int Index { get; }
        /// <summary>Gets the exact planned entry.</summary>
        public WorkspacePatchEntry Entry { get; }
        /// <summary>Gets the opened source or target parent.</summary>
        public SafeFileHandle Parent { get; }
        /// <summary>Gets the source or target leaf name.</summary>
        public string Name { get; }
        /// <summary>Gets or sets the opened move destination parent.</summary>
        public SafeFileHandle? DestinationParent { get; set; }
        /// <summary>Gets or sets the move destination leaf name.</summary>
        public string? DestinationName { get; set; }
        /// <summary>Gets or sets the mode captured for replacement preservation.</summary>
        public int Mode { get; set; }
        /// <summary>Gets or sets the derived private staging leaf name.</summary>
        public string? StagingName { get; set; }
        /// <summary>Gets or sets whether staging cleanup is still required.</summary>
        public bool StagingExists { get; set; }

        /// <summary>Removes uncommitted staging and releases owned descriptors.</summary>
        public void Dispose()
        {
            if (StagingExists && StagingName is not null)
            {
                _ = UnlinkAt(Parent.DangerousGetHandle().ToInt32(), StagingName, 0);
                StagingExists = false;
            }

            DestinationParent?.Dispose();
            Parent.Dispose();
        }
    }

    [LibraryImport("libc", EntryPoint = "renameat2", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int RenameAt2(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath,
        uint flags);

    [LibraryImport("libc", EntryPoint = "renameatx_np", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int RenameAtExclusive(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath,
        uint flags);
}
