// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// The default <see cref="IFileSystem"/>: resolves every
/// <see cref="FileSystemPath"/> against a configured root directory and
/// re-validates that the resolved absolute path stays within it before
/// performing any I/O.
/// </summary>
/// <remarks>
/// This class re-resolves and re-validates the path on every call; it never
/// trusts that a caller already checked containment. A path that resolves
/// outside the configured root is refused with <see cref="FileReadDenied"/>
/// or <see cref="FileWriteDenied"/> regardless of any higher-level
/// authorization decision that already ran, which is the low-level
/// boundary re-enforcing the same effect a higher-level allow cannot widen.
/// </remarks>
public sealed partial class SandboxedFileSystem:
    IFileSystem,
    IDirectoryReader,
    IFileGlobber,
    IFileContentSearcher,
    IFileSnapshotReader,
    IAtomicFileReplacer,
    IWorkspacePatchApplier
{
    private const int _errorAccessDenied = 13;
    private const int _errorAlreadyExists = 17;
    private const int _errorInvalidArgument = 22;
    private const int _errorNotDirectory = 20;
    private const int _errorNotFound = 2;
    private const int _linuxErrorTooManyLinks = 40;
    private const int _macOsErrorTooManyLinks = 62;
    private const int _openAppend = 0x0008;
    private const int _openReadOnly = 0;
    private const int _openWriteOnly = 0x0001;
    private const int _ownerReadWritePermissions = 0x0180;
    private const int _unixFilePermissions = 0x01B6;
    private const int _writeOpenAttempts = 4;

    private readonly string _root;
    private readonly long _maximumReadBytes;
    private readonly long _maximumWriteBytes;
    private readonly int _maximumDirectorySnapshotEntries;
    private readonly int _maximumSearchDepth;
    private readonly int _maximumSearchFiles;
    private readonly long _maximumSearchBytes;
    private readonly int _maximumSearchMatches;
    private readonly int _maximumSearchLineBytes;
    private readonly TimeSpan _maximumSearchDuration;
    private readonly int _maximumPatchEntries;
    private readonly long _maximumPatchBytes;
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ILogger<SandboxedFileSystem> _logger;

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.filesystem.sandboxed");

    /// <summary>Initializes a new instance of the <see cref="SandboxedFileSystem"/> class.</summary>
    /// <param name="options">The validated sandbox configuration.</param>
    /// <param name="grantStore">The authoritative grant store used immediately before host observations and mutations.</param>
    /// <param name="timeProvider">The monotonic time source used for elapsed search bounds.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><see cref="SandboxedFileSystemOptions.RootDirectory"/> is not an absolute path.</exception>
    public SandboxedFileSystem(
        IOptions<SandboxedFileSystemOptions> options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<SandboxedFileSystem>? logger = null)
        : this(options, grantStore, timeProvider, logger, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the sandboxed filesystem with a source of fresh per-effect enforcement-intent identities.</summary>
    /// <param name="options">The validated sandbox configuration.</param>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="timeProvider">The monotonic time source used for elapsed search bounds.</param>
    /// <param name="logger">The optional structured logger; a null value selects a null logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh filesystem enforcement intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>, <paramref name="grantStore"/>, <paramref name="timeProvider"/>, or <paramref name="intentIds"/> is null.</exception>
    /// <exception cref="ArgumentException"><see cref="SandboxedFileSystemOptions.RootDirectory"/> is blank or not an absolute path.</exception>
    public SandboxedFileSystem(
        IOptions<SandboxedFileSystemOptions> options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<SandboxedFileSystem>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);

        var root = options.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            throw new ArgumentException(
                "SandboxedFileSystemOptions.RootDirectory must be set to an absolute path.", nameof(options));
        }

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _maximumReadBytes = options.Value.MaximumReadBytes;
        _maximumWriteBytes = options.Value.MaximumWriteBytes;
        _maximumDirectorySnapshotEntries = options.Value.MaximumDirectorySnapshotEntries;
        _maximumSearchDepth = options.Value.MaximumSearchDepth;
        _maximumSearchFiles = options.Value.MaximumSearchFiles;
        _maximumSearchBytes = options.Value.MaximumSearchBytes;
        _maximumSearchMatches = options.Value.MaximumSearchMatches;
        _maximumSearchLineBytes = options.Value.MaximumSearchLineBytes;
        _maximumSearchDuration = options.Value.MaximumSearchDuration;
        _maximumPatchEntries = options.Value.MaximumPatchEntries;
        _maximumPatchBytes = options.Value.MaximumPatchBytes;
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _logger = logger ?? NullLogger<SandboxedFileSystem>.Instance;
    }

    /// <inheritdoc/>
    private async Task<FileReadResult> ReadCoreAsync(FileReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(request.Path)],
            FileSecurityBinding.ReadFingerprint(request.Path));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return new FileReadDenied(FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (!IsSecureTraversalSupported)
        {
            return new FileReadDenied("Secure no-follow file traversal is unavailable on this platform.");
        }

        if (!TryOpenParentDirectory(
                request.Path,
                cancellationToken,
                out var parent,
                out var fileName,
                out var traversalError))
        {
            return traversalError == _errorNotFound
                ? new FileNotFound(request.Path)
                : BoundaryReadFailure(request.Path, traversalError);
        }

        using (parent)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var descriptor = OpenAt(
                    parent.DangerousGetHandle().ToInt32(),
                    fileName,
                    _openReadOnly | NoFollowFlag | CloseOnExecFlag,
                    0);
                if (descriptor < 0)
                {
                    var error = Marshal.GetLastPInvokeError();
                    return error == _errorNotFound
                        ? new FileNotFound(request.Path)
                        : BoundaryReadFailure(request.Path, error);
                }

                using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
                await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
                using var content = new MemoryStream();
                var buffer = new byte[81920];
                long totalBytes = 0;

                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes += bytesRead;
                    if (totalBytes > _maximumReadBytes)
                    {
                        // A size ceiling is resource exhaustion, not an authorization outcome; the grant was valid.
                        return new FileReadFailed(
                            $"File exceeds the configured maximum of {_maximumReadBytes} bytes.");
                    }

                    await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }

                return new FileRead(Encoding.UTF8.GetString(content.GetBuffer(), 0, checked((int) content.Length)), totalBytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new FileReadFailed("The file could not be read.");
            }
        }
    }

    /// <inheritdoc/>
    private async Task<FileWriteResult> WriteCoreAsync(FileWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfUndefined(request.Mode);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSecureTraversalSupported)
        {
            return new FileWriteDenied("Secure no-follow file traversal is unavailable on this platform.");
        }

        var contentBytes = Encoding.UTF8.GetByteCount(request.Content);
        if (contentBytes > _maximumWriteBytes)
        {
            return new FileWriteDenied(
                $"Content is {contentBytes} bytes, exceeding the configured maximum of {_maximumWriteBytes}.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(request.Mode),
            [FileSecurityBinding.Resource(request.Path)],
            FileSecurityBinding.WriteFingerprint(request.Path, request.Content, request.Mode));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return new FileWriteDenied(FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryOpenParentDirectory(
                    request.Path,
                    cancellationToken,
                    out var parent,
                    out var fileName,
                    out var traversalError))
            {
                return BoundaryWriteFailure(request.Path, traversalError);
            }

            using (parent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (request.Mode == FileWriteMode.ReplaceExisting)
                {
                    return await ReplaceExistingWriteAsync(
                        parent,
                        fileName,
                        request.Path,
                        request.Content,
                        contentBytes,
                        enforcementIntent.Id,
                        reportBoundaryViolationsAsDenied: false,
                        cancellationToken).ConfigureAwait(false);
                }
                if (request.Mode == FileWriteMode.CreateOrOverwrite)
                {
                    return await CreateOrReplaceWriteAsync(
                        parent,
                        fileName,
                        request.Path,
                        request.Content,
                        contentBytes,
                        enforcementIntent.Id,
                        cancellationToken).ConfigureAwait(false);
                }
                if (!TryOpenWriteTarget(
                    parent.DangerousGetHandle().ToInt32(),
                    fileName,
                    request.Mode,
                    out var descriptor,
                    out var created,
                    out var openError))
                {
                    return request.Mode == FileWriteMode.CreateNew && openError == _errorAlreadyExists
                        ? new FileAlreadyExists(request.Path)
                        : BoundaryWriteFailure(request.Path, openError);
                }

                using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
                if (created)
                {
                    _ = ChangeMode(descriptor, _ownerReadWritePermissions);
                }

                await using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false);
                if (request.Mode == FileWriteMode.Append)
                {
                    _ = stream.Seek(0, SeekOrigin.End);
                }

                var bytes = Encoding.UTF8.GetBytes(request.Content);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return new FileWritten(contentBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileWriteFailed("The file could not be written.");
        }
    }

    private static async ValueTask<FileWriteResult> ReplaceExistingWriteAsync(
        SafeFileHandle parent,
        string fileName,
        FileSystemPath path,
        string content,
        int contentBytes,
        SecurityEnforcementIntentId intentId,
        bool reportBoundaryViolationsAsDenied,
        CancellationToken cancellationToken)
    {
        var parentDescriptor = parent.DangerousGetHandle().ToInt32();
        var currentDescriptor = OpenAt(
            parentDescriptor,
            fileName,
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (currentDescriptor < 0)
        {
            var openError = Marshal.GetLastPInvokeError();
            if (openError == _errorNotFound)
            {
                return new FileWriteFailed(_replacementTargetVanishedMessage);
            }

            // FileWriteMode.ReplaceExisting reports every open failure as a generic FileWriteFailed
            // (its documented contract makes no boundary/denial distinction); CreateOrReplaceWriteAsync
            // delegates here for FileWriteMode.CreateOrOverwrite's existing-target case and needs the
            // same FileWriteDenied a boundary violation produces everywhere else in this class.
            return reportBoundaryViolationsAsDenied
                ? BoundaryWriteFailure(path, openError)
                : new FileWriteFailed("The replacement target could not be opened.");
        }

        int mode;
        using (var current = new SafeFileHandle(new IntPtr(currentDescriptor), ownsHandle: true))
        {
            if (!TryGetFileMode(currentDescriptor, out mode))
            {
                return new FileWriteFailed("The target mode could not be observed.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stagingName = $".agentkit-write-{intentId.Value:N}.tmp";
        var stagingDescriptor = OpenAt(
            parentDescriptor,
            stagingName,
            _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
            _ownerReadWritePermissions);
        if (stagingDescriptor < 0)
        {
            return new FileWriteFailed("The replacement could not be staged.");
        }

        var stagingExists = true;
        try
        {
            using (var stagingHandle = new SafeFileHandle(new IntPtr(stagingDescriptor), ownsHandle: true))
            await using (var stream = new FileStream(
                stagingHandle, FileAccess.Write, bufferSize: 81920, isAsync: false))
            {
                if (ChangeMode(stagingDescriptor, mode & 0x0FFF) < 0)
                {
                    return new FileWriteFailed("The target mode could not be preserved.");
                }

                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stillExists = OpenAt(
                parentDescriptor,
                fileName,
                _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
                0);
            if (stillExists < 0)
            {
                return new FileWriteFailed("The replacement target no longer exists.");
            }
            using (var current = new SafeFileHandle(new IntPtr(stillExists), ownsHandle: true))
            {
            }

            if (RenameAt(parentDescriptor, stagingName, parentDescriptor, fileName) < 0)
            {
                return new FileWriteFailed("The staged replacement could not be committed.");
            }

            stagingExists = false;
            return new FileWritten(contentBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FileWriteFailed("The replacement could not be staged.");
        }
        finally
        {
            if (stagingExists)
            {
                _ = UnlinkAt(parentDescriptor, stagingName, 0);
            }
        }
    }

    /// <summary>The exact failure text <see cref="ReplaceExistingWriteAsync"/> reports when its target vanished
    /// between the caller's existence check and its own open, used by <see cref="CreateOrReplaceWriteAsync"/> to
    /// distinguish "retry as a fresh create" from every other replacement failure.</summary>
    private const string _replacementTargetVanishedMessage = "The replacement target does not exist.";

    /// <summary>
    /// Creates <paramref name="fileName"/> when it does not exist, or atomically replaces its content via the
    /// same staged write-then-rename <see cref="ReplaceExistingWriteAsync"/> uses when it does.
    /// </summary>
    /// <remarks>
    /// <see cref="FileWriteMode.CreateOrOverwrite"/> previously reopened an existing target with
    /// <c>O_WRONLY | O_TRUNC</c> and wrote into the truncated file in place: a cancellation observed during the
    /// write, a disk-full/IO error, or process death between the truncating open and the write left the target
    /// empty or partially written, and never restored the file it replaced. Routing the existing-target case
    /// through the staged replace path means every failure before the final <c>renameat</c> commit leaves the
    /// prior target completely unchanged, matching the atomic-replace contract <c>ReplaceExisting</c> already
    /// honors. The bounded retry mirrors <see cref="TryOpenWriteTarget"/>: if the target is removed by another
    /// writer in the narrow window between this method's own create-exclusive attempt failing with "already
    /// exists" and the replace attempt's own open, the replace fails with a "does not exist" outcome and this
    /// method retries the whole create-or-replace attempt rather than surfacing a transient race as a failure.
    /// </remarks>
    private static async ValueTask<FileWriteResult> CreateOrReplaceWriteAsync(
        SafeFileHandle parent,
        string fileName,
        FileSystemPath path,
        string content,
        int contentBytes,
        SecurityEnforcementIntentId intentId,
        CancellationToken cancellationToken)
    {
        var parentDescriptor = parent.DangerousGetHandle().ToInt32();
        for (var attempt = 0; attempt < _writeOpenAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = OpenAt(
                parentDescriptor,
                fileName,
                _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
                _unixFilePermissions);
            if (descriptor >= 0)
            {
                using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
                _ = ChangeMode(descriptor, _ownerReadWritePermissions);
                try
                {
                    await using var stream = new FileStream(handle, FileAccess.Write, bufferSize: 81920, isAsync: false);
                    var bytes = Encoding.UTF8.GetBytes(content);
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return new FileWriteFailed("The file could not be written.");
                }

                return new FileWritten(contentBytes);
            }

            var error = Marshal.GetLastPInvokeError();
            if (error != _errorAlreadyExists)
            {
                return BoundaryWriteFailure(path, error);
            }

            var replaced = await ReplaceExistingWriteAsync(
                parent, fileName, path, content, contentBytes, intentId, reportBoundaryViolationsAsDenied: true, cancellationToken)
                .ConfigureAwait(false);
            if (replaced is not FileWriteFailed { SafeMessage: _replacementTargetVanishedMessage })
            {
                return replaced;
            }
        }

        return new FileWriteFailed("The file could not be created or replaced after repeated concurrent modification.");
    }

    /// <inheritdoc/>
    private async ValueTask<DirectoryEnumerationResult> EnumerateCoreAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumEntries);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [DirectorySecurityBinding.Resource(request.Path)],
            DirectorySecurityBinding.Fingerprint(request.Path, request.MaximumEntries, request.Continuation));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return DirectoryFailure(DirectoryEnumerationStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (!IsSecureTraversalSupported)
        {
            return DirectoryFailure(
                DirectoryEnumerationStatus.Denied,
                "Secure no-follow directory traversal is unavailable on this platform.");
        }

        if (!TryOpenDirectory(request.Path, cancellationToken, out var directory, out var openError))
        {
            return openError == _errorNotFound
                ? DirectoryFailure(DirectoryEnumerationStatus.NotFound, "The directory does not exist.")
                : DirectoryFailure(
                    IsBoundaryViolation(openError) ? DirectoryEnumerationStatus.Denied : DirectoryEnumerationStatus.Failed,
                    IsBoundaryViolation(openError)
                        ? "The directory crosses a symbolic link or inaccessible boundary."
                        : "The directory could not be enumerated.");
        }

        using (directory)
        {
            try
            {
                if (!TryReadDirectoryNames(directory, cancellationToken, out var childNames, out _))
                {
                    return DirectoryFailure(DirectoryEnumerationStatus.Failed, "The directory could not be enumerated.");
                }

                var childPaths = new List<string>();
                foreach (var name in childNames)
                {
                    if (childPaths.Count == _maximumDirectorySnapshotEntries)
                    {
                        return DirectoryFailure(
                            DirectoryEnumerationStatus.LimitExceeded,
                            $"The directory exceeds the configured snapshot limit of {_maximumDirectorySnapshotEntries} entries.");
                    }

                    if (name.Contains('\\', StringComparison.Ordinal))
                    {
                        return DirectoryFailure(
                            DirectoryEnumerationStatus.Failed,
                            "The directory contains a name that cannot be represented by this path profile.");
                    }

                    childPaths.Add(request.Path is null ? name : $"{request.Path.Value.Value}/{name}");
                }

                childPaths.Sort(StringComparer.Ordinal);
                var snapshot = SnapshotFingerprint(childPaths);
                var start = request.Continuation?.NextIndex ?? 0;
                if (request.Continuation is not null
                    && (request.Continuation.SnapshotFingerprint != snapshot || start > childPaths.Count))
                {
                    return DirectoryFailure(
                        DirectoryEnumerationStatus.SnapshotChanged,
                        "The directory changed after the supplied continuation was issued.");
                }

                var retained = childPaths.Skip(start).Take(request.MaximumEntries)
                    .Select(static path => new DirectoryEntry(new FileSystemPath(path)))
                    .ToImmutableArray();
                var next = start + retained.Length;
                var continuation = next < childPaths.Count
                    ? new DirectoryEnumerationCursor(snapshot, next)
                    : null;
                return new DirectoryEnumerationResult(
                    DirectoryEnumerationStatus.Success,
                    retained,
                    snapshot,
                    continuation,
                    null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return DirectoryFailure(DirectoryEnumerationStatus.Failed, "The directory could not be enumerated.");
            }
        }
    }

    /// <inheritdoc/>
    private async ValueTask<GlobResult> GlobCoreAsync(GlobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Pattern.Value, "request.Pattern");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumResults);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [GlobSecurityBinding.Resource(request.BasePath)],
            GlobSecurityBinding.Fingerprint(
                request.BasePath,
                request.Pattern,
                request.CaseSensitive,
                request.IncludeHidden,
                request.MaximumDepth,
                request.MaximumVisitedEntries,
                request.MaximumResults,
                request.ExcludedPathPatterns));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return new GlobResult(GlobStatus.Denied, [], 0, false, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (!IsSecureTraversalSupported)
        {
            return new GlobResult(
                GlobStatus.Denied, [], 0, false, "Secure no-follow traversal is unavailable on this platform.");
        }

        if (!TryOpenDirectory(request.BasePath, cancellationToken, out var root, out var openError))
        {
            return openError == _errorNotFound
                ? new GlobResult(GlobStatus.NotFound, [], 0, true, "The glob base directory does not exist.")
                : new GlobResult(
                    IsBoundaryViolation(openError) ? GlobStatus.Denied : GlobStatus.Failed,
                    [],
                    0,
                    false,
                    IsBoundaryViolation(openError)
                        ? "The glob base crosses a symbolic link or inaccessible boundary."
                        : "The glob base could not be traversed.");
        }

        using (root)
        {
            var state = new GlobTraversalState(request);
            TraverseGlobDirectory(root, "", 1, state, cancellationToken);
            state.Matches.Sort(StringComparer.Ordinal);
            var matches = state.Matches.Select(static value => new FileSystemPath(value)).ToImmutableArray();
            return state.TerminalStatus is { } terminal
                ? new GlobResult(terminal, matches, state.VisitedEntries, false, state.SafeMessage)
                : matches.IsEmpty
                    ? new GlobResult(GlobStatus.NoMatches, [], state.VisitedEntries, true, "The glob completed with no matches.")
                    : new GlobResult(GlobStatus.Success, matches, state.VisitedEntries, true, null);
        }
    }

    private static void TraverseGlobDirectory(
        SafeFileHandle directory,
        string relativeParent,
        int depth,
        GlobTraversalState state,
        CancellationToken cancellationToken)
    {
        if (state.TerminalStatus is not null)
        {
            return;
        }

        if (!TryReadDirectoryNames(directory, cancellationToken, out var names, out _))
        {
            state.Fail(GlobStatus.Failed, "A directory could not be enumerated.");
            return;
        }

        names.Sort(StringComparer.Ordinal);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!state.Request.IncludeHidden && name.StartsWith('.'))
            {
                continue;
            }

            var relative = relativeParent.Length == 0 ? name : $"{relativeParent}/{name}";
            state.VisitedEntries++;
            if (state.VisitedEntries > state.Request.MaximumVisitedEntries)
            {
                state.Fail(GlobStatus.LimitExceeded, "The glob visited-entry limit was exceeded.");
                return;
            }

            if (IsExcludedPath(relative, state.Request.ExcludedPathPatterns, state.Request.CaseSensitive))
            {
                continue;
            }

            var workspacePath = state.Request.BasePath is null
                ? relative
                : $"{state.Request.BasePath.Value.Value}/{relative}";
            if (GlobMatches(state.Request.Pattern.Value, relative, state.Request.CaseSensitive))
            {
                if (state.Matches.Count == state.Request.MaximumResults)
                {
                    state.Fail(GlobStatus.LimitExceeded, "The glob retained-result limit was exceeded.");
                    return;
                }

                state.Matches.Add(workspacePath);
            }

            var descriptor = OpenAt(
                directory.DangerousGetHandle().ToInt32(),
                name,
                _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);
            if (descriptor < 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is _errorNotDirectory || error == (OperatingSystem.IsMacOS()
                        ? _macOsErrorTooManyLinks
                        : _linuxErrorTooManyLinks))
                {
                    continue;
                }

                if (IsBoundaryViolation(error))
                {
                    state.Fail(GlobStatus.Denied, "A directory entry crossed an inaccessible boundary.");
                    return;
                }

                continue;
            }

            using var child = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            if (depth < state.Request.MaximumDepth)
            {
                TraverseGlobDirectory(child, relative, depth + 1, state, cancellationToken);
                if (state.TerminalStatus is not null)
                {
                    return;
                }
            }
        }
    }

    private static bool GlobMatches(string pattern, string path, bool caseSensitive)
    {
        var patternSegments = pattern.Split('/');
        var pathSegments = path.Split('/');
        return MatchGlobSegments(patternSegments, 0, pathSegments, 0, caseSensitive);
    }

    private static bool IsExcludedPath(
        string path,
        ImmutableArray<GlobPattern> excludedPathPatterns,
        bool caseSensitive)
    {
        if (excludedPathPatterns.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var pattern in excludedPathPatterns)
        {
            var value = pattern.Value;
            if (GlobMatches(value, path, caseSensitive)
                || (value.EndsWith("/**", StringComparison.Ordinal)
                    && GlobMatches(value[..^3], path, caseSensitive)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchGlobSegments(
        string[] pattern,
        int patternIndex,
        string[] path,
        int pathIndex,
        bool caseSensitive)
    {
        return patternIndex == pattern.Length
            ? pathIndex == path.Length
            : pattern[patternIndex] == "**"
                ? MatchGlobSegments(pattern, patternIndex + 1, path, pathIndex, caseSensitive)
                    || (pathIndex < path.Length
                        && MatchGlobSegments(pattern, patternIndex, path, pathIndex + 1, caseSensitive))
                : pathIndex < path.Length
                    && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        pattern[patternIndex], path[pathIndex], ignoreCase: !caseSensitive)
                    && MatchGlobSegments(pattern, patternIndex + 1, path, pathIndex + 1, caseSensitive);
    }

    private sealed class GlobTraversalState(GlobRequest request)
    {
        public GlobRequest Request { get; } = request;
        public List<string> Matches { get; } = [];
        public int VisitedEntries { get; set; }
        public GlobStatus? TerminalStatus { get; private set; }
        public string? SafeMessage { get; private set; }

        public void Fail(GlobStatus status, string message)
        {
            TerminalStatus = status;
            SafeMessage = message;
        }
    }

    private static bool TryReadDirectoryNames(
        SafeFileHandle directory,
        CancellationToken cancellationToken,
        out List<string> names,
        out int error)
    {
        names = [];
        var duplicate = DuplicateDescriptor(directory.DangerousGetHandle().ToInt32());
        if (duplicate < 0)
        {
            error = Marshal.GetLastPInvokeError();
            return false;
        }

        var stream = OpenDirectoryStream(duplicate);
        if (stream == IntPtr.Zero)
        {
            error = Marshal.GetLastPInvokeError();
            _ = CloseDescriptor(duplicate);
            return false;
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Marshal.SetLastPInvokeError(0);
                var entry = ReadDirectoryEntry(stream);
                if (entry == IntPtr.Zero)
                {
                    error = Marshal.GetLastPInvokeError();
                    return error == 0;
                }

                var nameOffset = OperatingSystem.IsMacOS() ? 21 : 19;
                var name = Marshal.PtrToStringUTF8(IntPtr.Add(entry, nameOffset));
                if (name is null)
                {
                    error = _errorInvalidArgument;
                    return false;
                }

                if (name is not "." and not "..")
                {
                    names.Add(name);
                }
            }
        }
        finally
        {
            _ = CloseDirectoryStream(stream);
        }
    }

    private static DirectoryEnumerationResult DirectoryFailure(DirectoryEnumerationStatus status, string message) =>
        new(status, [], null, null, message);

    private static ContentHash SnapshotFingerprint(IEnumerable<string> paths)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var path in paths)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        return new ContentHash($"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}");
    }

    private bool TryOpenDirectory(
        FileSystemPath? path,
        CancellationToken cancellationToken,
        out SafeFileHandle directory,
        out int error)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (path is null)
        {
            var rootDescriptor = Open(_root, _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag, 0);
            directory = rootDescriptor >= 0
                ? new SafeFileHandle(new IntPtr(rootDescriptor), ownsHandle: true)
                : new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            error = rootDescriptor >= 0 ? 0 : Marshal.GetLastPInvokeError();
            return rootDescriptor >= 0;
        }

        if (!TryOpenParentDirectory(path.Value, cancellationToken, out var parent, out var name, out error))
        {
            directory = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            return false;
        }

        using (parent)
        {
            var descriptor = OpenAt(
                parent.DangerousGetHandle().ToInt32(),
                name,
                _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);
            directory = descriptor >= 0
                ? new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true)
                : new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            error = descriptor >= 0 ? 0 : Marshal.GetLastPInvokeError();
            return descriptor >= 0;
        }
    }

    private static bool IsSecureTraversalSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    private static int CloseOnExecFlag => OperatingSystem.IsMacOS() ? 0x01000000 : 0x00080000;

    private static int CreateFlag => OperatingSystem.IsMacOS() ? 0x0200 : 0x0040;

    private static int DirectoryFlag => OperatingSystem.IsMacOS() ? 0x00100000 : 0x00010000;

    private static int ExclusiveFlag => OperatingSystem.IsMacOS() ? 0x0800 : 0x0080;

    private static int NoFollowFlag => OperatingSystem.IsMacOS() ? 0x0100 : 0x00020000;

    private static int TruncateFlag => OperatingSystem.IsMacOS() ? 0x0400 : 0x0200;

    private static bool IsBoundaryViolation(int error) =>
        error is _errorAccessDenied or _errorNotDirectory
        || error == (OperatingSystem.IsMacOS() ? _macOsErrorTooManyLinks : _linuxErrorTooManyLinks);

    private static FileReadResult BoundaryReadFailure(FileSystemPath path, int error) =>
        IsBoundaryViolation(error)
            ? new FileReadDenied($"Path '{path}' crosses a symbolic link or an inaccessible boundary.")
            : new FileReadFailed("The file could not be read.");

    private static FileWriteResult BoundaryWriteFailure(FileSystemPath path, int error) =>
        IsBoundaryViolation(error)
            ? new FileWriteDenied($"Path '{path}' crosses a symbolic link or an inaccessible boundary.")
            : new FileWriteFailed("The file could not be written.");

    private bool TryOpenParentDirectory(
        FileSystemPath path,
        CancellationToken cancellationToken,
        out SafeFileHandle parent,
        out string fileName,
        out int error)
    {
        var segments = path.Value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[^1] == ".")
        {
            parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            fileName = "";
            error = _errorInvalidArgument;
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = Open(
            _root,
            _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
            0);
        if (descriptor < 0)
        {
            parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            fileName = "";
            error = Marshal.GetLastPInvokeError();
            return false;
        }

        var current = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            descriptor = OpenAt(
                current.DangerousGetHandle().ToInt32(),
                segments[index],
                _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);

            if (descriptor < 0)
            {
                error = Marshal.GetLastPInvokeError();
                current.Dispose();
                parent = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
                fileName = "";
                return false;
            }

            var next = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
            current.Dispose();
            current = next;
        }

        parent = current;
        fileName = segments[^1];
        error = 0;
        return true;
    }

    // Only FileWriteMode.CreateNew and FileWriteMode.Append reach this method: WriteCoreAsync routes
    // ReplaceExisting through ReplaceExistingWriteAsync and CreateOrOverwrite through
    // CreateOrReplaceWriteAsync, both of which stage-and-rename instead of truncating an existing
    // target in place. CreateNew's O_CREAT | O_EXCL open fails outright on "already exists" (it must
    // never touch an existing target), and Append's O_APPEND open without O_CREAT can only fail with
    // "not found", so neither mode ever needs the reopen-existing retry the pre-fix implementation had.
    private static bool TryOpenWriteTarget(
        int parentDescriptor,
        string fileName,
        FileWriteMode mode,
        out int descriptor,
        out bool created,
        out int error)
    {
        descriptor = OpenAt(parentDescriptor, fileName, NewFileOpenFlags(mode), _unixFilePermissions);
        if (descriptor >= 0)
        {
            // Append opens without O_CREAT, so a successful open means the target already existed.
            created = mode != FileWriteMode.Append;
            error = 0;
            return true;
        }

        error = Marshal.GetLastPInvokeError();
        created = false;
        return false;
    }

    private static int NewFileOpenFlags(FileWriteMode mode) => mode switch
    {
        FileWriteMode.CreateNew => _openWriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
        FileWriteMode.CreateOrOverwrite or FileWriteMode.ReplaceExisting => throw new UnreachableException(),
        // Append never creates: the disposition table requires "not found, no mutation" for a missing target,
        // so the new-file open is attempted without O_CREAT and fails with ENOENT when the target is absent.
        FileWriteMode.Append => _openWriteOnly | _openAppend | NoFollowFlag | CloseOnExecFlag,
        _ => throw new UnreachableException()
    };

    [LibraryImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static partial int ChangeMode(int descriptor, int mode);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int CloseDescriptor(int descriptor);

    [LibraryImport("libc", EntryPoint = "closedir", SetLastError = true)]
    private static partial int CloseDirectoryStream(IntPtr stream);

    [LibraryImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static partial int DuplicateDescriptor(int descriptor);

    /// <summary>
    /// Whether the process must bind libc's <c>$INODE64</c>-suffixed <c>fdopendir</c>/<c>readdir</c>
    /// symbols directly.
    /// </summary>
    /// <remarks>
    /// On x86_64 macOS, the plain <c>fdopendir</c>/<c>readdir</c> dynamic symbols are the legacy
    /// 32-bit-inode entry points kept for binary compatibility; a C compiler silently redirects
    /// source-level calls to the <c>$INODE64</c> symbols via a header macro, but a raw
    /// <c>dlsym</c>-style P/Invoke lookup (what <see cref="LibraryImportAttribute"/> performs) binds
    /// the literal, legacy symbol and returns a <c>struct dirent</c> whose <c>d_name</c> offset does
    /// not match the one <see cref="TryReadDirectoryNames"/> uses. Arm64 macOS has only one struct
    /// layout and exposes no <c>$INODE64</c>-suffixed symbols, and Linux has no such symbol pair at
    /// all, so the explicit binding is required only for this one combination.
    /// </remarks>
    private static readonly bool _requiresMacOsInode64DirectorySymbols =
        OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    private static IntPtr OpenDirectoryStream(int descriptor) => _requiresMacOsInode64DirectorySymbols
        ? OpenDirectoryStreamInode64(descriptor)
        : OpenDirectoryStreamDefault(descriptor);

    private static IntPtr ReadDirectoryEntry(IntPtr stream) => _requiresMacOsInode64DirectorySymbols
        ? ReadDirectoryEntryInode64(stream)
        : ReadDirectoryEntryDefault(stream);

    [LibraryImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
    private static partial IntPtr OpenDirectoryStreamDefault(int descriptor);

    [LibraryImport("libc", EntryPoint = "fdopendir$INODE64", SetLastError = true)]
    private static partial IntPtr OpenDirectoryStreamInode64(int descriptor);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenAt(int directoryDescriptor, string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "readdir", SetLastError = true)]
    private static partial IntPtr ReadDirectoryEntryDefault(IntPtr stream);

    [LibraryImport("libc", EntryPoint = "readdir$INODE64", SetLastError = true)]
    private static partial IntPtr ReadDirectoryEntryInode64(IntPtr stream);
}
