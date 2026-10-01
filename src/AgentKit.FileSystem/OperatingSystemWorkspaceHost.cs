// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using static AgentKit.FileSystem.PosixFileOperations;

/// <summary>
/// Operating-system workspace operations for one keyed profile: directory enumeration, glob, content search, snapshot
/// reads, version-conditional atomic replacement, and atomic multi-file patches.
/// </summary>
/// <remarks>
/// <para>
/// The host resolves every <see cref="FileSystemPath"/> against the profile's first registered root and re-validates, with
/// no-follow descriptor traversal, that the resolved target stays within it before performing any I/O. It re-resolves and
/// re-validates on every call and never trusts that a caller already checked containment, so a path that crosses a
/// symbolic link or an inaccessible boundary is refused regardless of any higher-level authorization decision. That is
/// the low-level boundary re-enforcing the same effect a higher-level allow cannot widen.
/// </para>
/// <para>
/// Every effect validates and consumes its <see cref="SecurityGrant"/> against exact resource and input evidence, with a
/// fresh enforcement intent, immediately before the host observes or mutates anything. Instances are safe for concurrent
/// use; mutation plans over the same path are serialized by an internal per-path queue.
/// </para>
/// </remarks>
internal sealed partial class OperatingSystemWorkspaceHost:
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
    private const byte _directoryEntryTypeDirectory = 4;
    private const byte _directoryEntryTypeUnknown = 0;

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
    private readonly ILogger<OperatingSystemWorkspaceHost> _logger;

    /// <summary>Gets the component identity to which workspace-operation grants must be addressed.</summary>
    /// <value>The profile-scoped operating-system audience shared with the profile's reader and writer.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Initializes a workspace host bound to one profile snapshot.</summary>
    /// <param name="profile">The immutable profile snapshot supplying the root and every bound.</param>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="timeProvider">The monotonic time source used for elapsed search bounds.</param>
    /// <param name="logger">The optional structured logger; a null value selects a null logger.</param>
    /// <param name="intentIds">The thread-safe source of fresh per-effect enforcement-intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/>, <paramref name="grantStore"/>, <paramref name="timeProvider"/>, or <paramref name="intentIds"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profile"/> declares no root.</exception>
    public OperatingSystemWorkspaceHost(
        OperatingSystemFileSystemOptionsSnapshot profile,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<OperatingSystemWorkspaceHost>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentException.ThrowIfDefaultOrEmpty(profile.Roots, nameof(profile));

        SecurityAudience = new ComponentId($"agentkit.filesystem.os.{profile.ProfileKey.Value}");
        _root = profile.Roots[0].HostRootPath;
        _maximumReadBytes = profile.Bounds.MaximumReadBytes;
        _maximumWriteBytes = profile.Bounds.MaximumWriteBytes;
        _maximumDirectorySnapshotEntries = profile.WorkspaceBounds.MaximumDirectorySnapshotEntries;
        _maximumSearchDepth = profile.WorkspaceBounds.MaximumSearchDepth;
        _maximumSearchFiles = profile.WorkspaceBounds.MaximumSearchFiles;
        _maximumSearchBytes = profile.WorkspaceBounds.MaximumSearchBytes;
        _maximumSearchMatches = profile.WorkspaceBounds.MaximumSearchMatches;
        _maximumSearchLineBytes = profile.WorkspaceBounds.MaximumSearchLineBytes;
        _maximumSearchDuration = profile.WorkspaceBounds.MaximumSearchDuration;
        _maximumPatchEntries = profile.WorkspaceBounds.MaximumPatchEntries;
        _maximumPatchBytes = profile.WorkspaceBounds.MaximumPatchBytes;
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _logger = logger ?? NullLogger<OperatingSystemWorkspaceHost>.Instance;
    }

    /// <summary>Observes one directory after exact lower-boundary grant consumption.</summary>
    /// <param name="operation">The authorized enumeration evidence.</param>
    /// <param name="cancellationToken">Cancels before the observation settles.</param>
    /// <returns>The classified outcome; failures never throw except cancellation.</returns>
    private async ValueTask<DirectoryReadOutcome> EnumerateCoreAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken)
    {
        Debug.Assert(operation is not null, "The public boundary validates the operation.");
        cancellationToken.ThrowIfCancellationRequested();

        var relative = operation.ResolvedTarget.RelativePath.Value;
        FileSystemPath? path = relative is "." ? null : new FileSystemPath(relative);
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [DirectorySecurityBinding.Resource(path)],
            DirectorySecurityBinding.Fingerprint(path));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            operation.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, operation.Grant, enforcement, enforcementIntent))
        {
            return DirectoryReadOutcome.Failure(DirectoryReadStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (!IsSecureTraversalSupported)
        {
            return DirectoryReadOutcome.Failure(
                DirectoryReadStatus.Denied,
                "Secure no-follow directory traversal is unavailable on this platform.");
        }

        if (!TryOpenDirectory(path, cancellationToken, out var directory, out var openError))
        {
            return openError == _errorNotFound
                ? DirectoryReadOutcome.Failure(DirectoryReadStatus.NotFound, "The directory does not exist.")
                : IsBoundaryViolation(openError)
                    ? DirectoryReadOutcome.Failure(DirectoryReadStatus.Denied, "The directory crosses a symbolic link or inaccessible boundary.")
                    : DirectoryReadOutcome.Failure(DirectoryReadStatus.Failed, "The directory could not be enumerated.");
        }

        using (directory)
        {
            try
            {
                if (!TryReadDirectoryEntries(directory, cancellationToken, out var children, out _))
                {
                    return DirectoryReadOutcome.Failure(DirectoryReadStatus.Failed, "The directory could not be enumerated.");
                }

                var entries = ImmutableArray.CreateBuilder<FileSystemEntry>();
                foreach (var (name, isDirectory) in children.OrderBy(static child => child.Name, StringComparer.Ordinal))
                {
                    if (entries.Count == _maximumDirectorySnapshotEntries)
                    {
                        return DirectoryReadOutcome.Failure(
                            DirectoryReadStatus.LimitExceeded,
                            $"The directory exceeds the configured snapshot limit of {_maximumDirectorySnapshotEntries} entries.");
                    }

                    if (NameIsUnrepresentable(name))
                    {
                        return DirectoryReadOutcome.Failure(
                            DirectoryReadStatus.Failed,
                            "The directory contains a name that cannot be represented by this path profile.");
                    }

                    entries.Add(new FileSystemEntry(new NormalizedRelativePath(name), isDirectory));
                }

                return DirectoryReadOutcome.Success(entries.ToImmutable());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return DirectoryReadOutcome.Failure(DirectoryReadStatus.Failed, "The directory could not be enumerated.");
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

            if (NameIsUnrepresentable(name))
            {
                state.Fail(GlobStatus.Failed, "The glob visited a name that cannot be represented by this path profile.");
                return;
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

    private static bool TryReadDirectoryEntries(
        SafeFileHandle directory,
        CancellationToken cancellationToken,
        out List<(string Name, bool IsDirectory)> children,
        out int error)
    {
        children = [];
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

                var name = Marshal.PtrToStringUTF8(IntPtr.Add(entry, OperatingSystem.IsMacOS() ? 21 : 19));
                if (name is null)
                {
                    error = _errorInvalidArgument;
                    return false;
                }

                if (name is not "." and not "..")
                {
                    var entryType = Marshal.ReadByte(entry, OperatingSystem.IsMacOS() ? 20 : 18);
                    children.Add((name, IsDirectoryEntry(directory, name, entryType)));
                }
            }
        }
        finally
        {
            _ = CloseDirectoryStream(stream);
        }
    }

    private static bool TryReadDirectoryNames(
        SafeFileHandle directory,
        CancellationToken cancellationToken,
        out List<string> names,
        out int error)
    {
        var read = TryReadDirectoryEntries(directory, cancellationToken, out var children, out error);
        names = [.. children.Select(static child => child.Name)];
        return read;
    }

    /// <summary>Classifies one directory entry as a directory without following symbolic links.</summary>
    /// <param name="parent">The open parent directory.</param>
    /// <param name="name">The entry name within <paramref name="parent"/>.</param>
    /// <param name="entryType">The <c>d_type</c> reported by the directory stream.</param>
    /// <returns>True when the entry is itself a directory; a symbolic link is never classified as one.</returns>
    /// <remarks>Filesystems that report an unknown type are resolved by opening the entry with no-follow directory semantics.</remarks>
    private static bool IsDirectoryEntry(SafeFileHandle parent, string name, byte entryType)
    {
        if (entryType != _directoryEntryTypeUnknown)
        {
            return entryType == _directoryEntryTypeDirectory;
        }

        var descriptor = OpenAt(
            parent.DangerousGetHandle().ToInt32(),
            name,
            _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
            0);
        if (descriptor < 0)
        {
            return false;
        }

        _ = CloseDescriptor(descriptor);
        return true;
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

    /// <summary>
    /// Determines whether a host-reported directory entry name cannot be represented as a
    /// <see cref="FileSystemPath"/> segment: a backslash would be silently reinterpreted as a path
    /// separator by <see cref="FileSystemPath"/>'s constructor, and a whitespace-only name fails that
    /// constructor's non-whitespace invariant. Any sandboxed process can create such a name on a
    /// case-sensitive, otherwise-permissive host file system, so callers must treat it as a typed
    /// enumeration/traversal failure instead of letting the resulting <see cref="ArgumentException"/>
    /// escape after the security grant has already been consumed.
    /// </summary>
    private static bool NameIsUnrepresentable(string name) =>
        name.Contains('\\', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(name);

    private bool TryOpenParentDirectory(
        FileSystemPath path,
        CancellationToken cancellationToken,
        out SafeFileHandle parent,
        out string fileName,
        out int error) =>
        PosixFileOperations.TryOpenParentDirectory(_root, path.Value, cancellationToken, out parent, out fileName, out error);

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
    /// not match the one <see cref="TryReadDirectoryEntries"/> uses. Arm64 macOS has only one struct
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

    [LibraryImport("libc", EntryPoint = "readdir", SetLastError = true)]
    private static partial IntPtr ReadDirectoryEntryDefault(IntPtr stream);

    [LibraryImport("libc", EntryPoint = "readdir$INODE64", SetLastError = true)]
    private static partial IntPtr ReadDirectoryEntryInode64(IntPtr stream);
}
