// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

using System.Diagnostics;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    private async ValueTask<WorkspacePatchResult> ApplyPatchCoreAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken)
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

        for (var index = 0; index < request.Entries.Length; index++)
        {
            var grantFailure = await ValidatePatchGrantAsync(request.Entries[index], cancellationToken).ConfigureAwait(false);
            if (grantFailure is not null)
            {
                return RejectPatch(request, $"Entry {index} was denied: {grantFailure}");
            }
        }

        lock (_gate)
        {
            for (var index = 0; index < request.Entries.Length; index++)
            {
                var failure = ValidatePatchPrecondition(request.Entries[index]);
                if (failure is not null)
                {
                    return RejectPatch(request, $"Entry {index} failed preflight: {failure}");
                }
            }

            var committed = 0;
            try
            {
                foreach (var entry in request.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CommitPatchEntry(entry);
                    committed++;
                }
            }
            catch (OperationCanceledException) when (committed > 0)
            {
                return SettlePatch(request, committed, "Cancellation arrived after a committed patch prefix.");
            }

            return SettlePatch(request, committed, null);
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

    private async ValueTask<string?> ValidatePatchGrantAsync(WorkspacePatchEntry entry, CancellationToken cancellationToken)
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
                    replace.Id, replace.Path, replace.ExpectedContentFingerprint, replace.Content)),
            WorkspacePatchDelete delete => (
                SecurityEffect.Delete,
                WorkspacePatchSecurityBinding.DeleteResources(delete.Path),
                WorkspacePatchSecurityBinding.DeleteFingerprint(delete.Path, delete.ExpectedContentFingerprint)),
            WorkspacePatchMove move => (
                SecurityEffect.Move,
                WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath),
                WorkspacePatchSecurityBinding.MoveFingerprint(
                    move.SourcePath, move.DestinationPath, move.ExpectedContentFingerprint)),
            _ => throw new UnreachableException(),
        };

        var enforcement = FileSystemEnforcementReceipt.Create(
            entry.Grant, SecurityAudience, SecurityOperationKind.FileWrite, effect, resources, fingerprint);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var result = await _grantStore.ValidateAndConsumeAsync(entry.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return FileSystemEnforcementReceipt.IsFreshExact(result, entry.Grant, enforcement, intent)
            ? null
            : FileSystemEnforcementReceipt.DenialMessage(result);
    }

    /// <summary>Checks one entry's precondition against the current in-memory tree. Must run under <see cref="_gate"/>.</summary>
    private string? ValidatePatchPrecondition(WorkspacePatchEntry entry)
    {
        return entry switch
        {
            WorkspacePatchCreate create => ParentMissing(create.Path) ?? (TargetAbsent(create.Path) ? null : "The target already exists."),
            WorkspacePatchReplace replace => ParentMissing(replace.Path) ?? RequireMatchingFingerprint(replace.Path, replace.ExpectedContentFingerprint),
            WorkspacePatchDelete delete => ParentMissing(delete.Path) ?? RequireMatchingFingerprint(delete.Path, delete.ExpectedContentFingerprint),
            WorkspacePatchMove move => ParentMissing(move.SourcePath)
                                ?? RequireMatchingFingerprint(move.SourcePath, move.ExpectedContentFingerprint)
                                ?? ParentMissing(move.DestinationPath)
                                ?? (TargetAbsent(move.DestinationPath) ? null : "The move destination already exists."),
            _ => throw new UnreachableException(),
        };
    }

    private void CommitPatchEntry(WorkspacePatchEntry entry)
    {
        switch (entry)
        {
            case WorkspacePatchCreate create:
                _files[create.Path.Value] = create.Content;
                break;
            case WorkspacePatchReplace replace:
                _files[replace.Path.Value] = replace.Content;
                break;
            case WorkspacePatchDelete delete:
                _ = _files.Remove(delete.Path.Value);
                break;
            case WorkspacePatchMove move:
                var content = _files[move.SourcePath.Value];
                _ = _files.Remove(move.SourcePath.Value);
                _files[move.DestinationPath.Value] = content;
                break;
            default:
                throw new UnreachableException();
        }
    }

    private string? ParentMissing(FileSystemPath path)
    {
        var parent = ParentDirectory(path.Value);
        return parent is not null && !_directories.Contains(parent) ? "A required parent directory does not exist." : null;
    }

    private bool TargetAbsent(FileSystemPath path) => !_files.ContainsKey(path.Value) && !_directories.Contains(path.Value);

    private string? RequireMatchingFingerprint(FileSystemPath path, ContentHash expected) =>
        _files.TryGetValue(path.Value, out var current)
            ? FileSecurityBinding.ContentFingerprint(current.AsSpan()) == expected
                ? null
                : "The target version changed."
            : "The required file does not exist.";

    private static WorkspacePatchResult RejectPatch(WorkspacePatchRequest request, string message) =>
        new(WorkspacePatchStatus.RejectedBeforeEffect, PatchResults(request, 0), message);

    private static WorkspacePatchResult SettlePatch(WorkspacePatchRequest request, int committed, string? message)
    {
        var status = committed == request.Entries.Length
            ? committed == 1 ? WorkspacePatchStatus.AtomicCommitted : WorkspacePatchStatus.CommittedWithNonAtomicVisibility
            : committed == 0
                ? WorkspacePatchStatus.RejectedBeforeEffect
                : WorkspacePatchStatus.Partial;
        return new WorkspacePatchResult(status, PatchResults(request, committed), message);
    }

    private static ImmutableArray<WorkspacePatchEntryResult> PatchResults(WorkspacePatchRequest request, int committed)
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

    private static IEnumerable<string> PatchPaths(WorkspacePatchEntry entry)
    {
        yield return PatchSourcePath(entry).Value;
        if (entry is WorkspacePatchMove move)
        {
            yield return move.DestinationPath.Value;
        }
    }
}
