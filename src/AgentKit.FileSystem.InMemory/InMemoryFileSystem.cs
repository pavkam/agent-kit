// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

using Microsoft.Extensions.Options;

/// <summary>
/// A deterministic, disk-free <see cref="IFileSystem"/>: resolves every
/// <see cref="FileSystemPath"/> against a process-local virtual tree that
/// exists only for the lifetime of this instance.
/// </summary>
/// <remarks>
/// <para>
/// This class exists to give tests and ephemeral hosts a fast, hermetic
/// double for <c>SandboxedFileSystem</c> that proves the same <see cref="IFileSystem"/>,
/// <see cref="ILegacyDirectoryReader"/>, <see cref="IFileGlobber"/>,
/// <see cref="IFileContentSearcher"/>, <see cref="IFileSnapshotReader"/>,
/// <see cref="IAtomicFileReplacer"/>, and <see cref="IWorkspacePatchApplier"/>
/// contracts without touching real disk. Every effect still validates and
/// consumes a <see cref="SecurityGrant"/> exactly as the disk-backed
/// implementation does; only the storage backend and its concurrency
/// mechanism differ. There are no symbolic links in the virtual tree, so the
/// boundary-crossing failure modes that exist purely to defend a real
/// filesystem against symlink traversal do not apply here.
/// </para>
/// <para>
/// There is no directory-creation member on any of the implemented
/// contracts, so a directory can only come to exist through
/// <see cref="CreateDirectory(FileSystemPath)"/> or as an implicit parent of
/// a path <see cref="Seed(FileSystemPath, string)"/> installs. Both bypass
/// grant validation entirely: they play the same role that directly writing
/// to the sandbox's configured root directory (outside any
/// <c>SandboxedFileSystem</c> instance method) plays in that implementation's
/// own tests. Every subsequent <see cref="IFileSystem"/> effect against the
/// resulting tree is fully protected.
/// </para>
/// </remarks>
[Obsolete("Use narrow host capability contracts selected through IFileSystemSelector instead.")]
public sealed partial class InMemoryFileSystem:
    IFileSystem,
    ILegacyDirectoryReader,
    IFileGlobber,
    IFileContentSearcher,
    IFileSnapshotReader,
    IAtomicFileReplacer,
    IWorkspacePatchApplier,
    IFileReader,
    IFileWriter,
    IFileMetadataReader,
    IDirectoryCreator,
    IDirectoryReader
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ImmutableArray<byte>> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

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
    private readonly ILogger<InMemoryFileSystem> _logger;
    private readonly ISecurityAuditDispatcher? _hostAuditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId>? _hostAuditRecordIds;

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <summary>Initializes a new instance of the <see cref="InMemoryFileSystem"/> class.</summary>
    /// <param name="options">The validated bound configuration.</param>
    /// <param name="grantStore">The authoritative grant store used immediately before every observation and mutation.</param>
    /// <param name="timeProvider">The monotonic time source used for elapsed search bounds.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public InMemoryFileSystem(
        IOptions<InMemoryFileSystemOptions> options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<InMemoryFileSystem>? logger = null)
        : this(options, grantStore, timeProvider, logger, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the in-memory filesystem with a source of fresh per-effect enforcement-intent identities.</summary>
    /// <param name="options">The validated bound configuration.</param>
    /// <param name="grantStore">The authoritative store that atomically consumes a grant and records permission to start.</param>
    /// <param name="timeProvider">The monotonic time source used for elapsed search bounds.</param>
    /// <param name="logger">The optional structured logger; a null value selects a null logger.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh filesystem enforcement intent identities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>, <paramref name="grantStore"/>, <paramref name="timeProvider"/>, or <paramref name="intentIds"/> is null.</exception>
    public InMemoryFileSystem(
        IOptions<InMemoryFileSystemOptions> options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<InMemoryFileSystem>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
        : this(options, grantStore, timeProvider, logger, intentIds, hostAuditDispatcher: null, hostAuditRecordIds: null, hostProfileKey: null)
    {
    }

    /// <summary>Initializes an in-memory volume that may expose spec host capability contracts.</summary>
    /// <param name="options">The validated bound configuration.</param>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="timeProvider">The monotonic clock.</param>
    /// <param name="logger">The optional logger.</param>
    /// <param name="intentIds">The enforcement-intent identity generator.</param>
    /// <param name="hostAuditDispatcher">The audit dispatcher required for host capabilities.</param>
    /// <param name="hostAuditRecordIds">The audit record identity generator.</param>
    /// <param name="hostProfileKey">The profile key when registered as a keyed host volume.</param>
    public InMemoryFileSystem(
        IOptions<InMemoryFileSystemOptions> options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<InMemoryFileSystem>? logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ISecurityAuditDispatcher? hostAuditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId>? hostAuditRecordIds,
        FileSystemProfileKey? hostProfileKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);

        SecurityAudience = hostProfileKey is { } profileKey
            ? new ComponentId($"agentkit.filesystem.inmemory.{profileKey.Value}")
            : new ComponentId("agentkit.filesystem.inmemory");
        _hostAuditDispatcher = hostAuditDispatcher;
        _hostAuditRecordIds = hostAuditRecordIds;

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
        _logger = logger ?? NullLogger<InMemoryFileSystem>.Instance;
    }

    /// <summary>Directly installs file bytes into the virtual tree, bypassing grant validation.</summary>
    /// <param name="path">The path to install, whose parent must already be an existing directory.</param>
    /// <param name="content">The exact bytes to install; replaces any existing file at <paramref name="path"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// The parent directory does not exist, or a directory already occupies <paramref name="path"/>.
    /// </exception>
    public void Seed(FileSystemPath path, ReadOnlySpan<byte> content)
    {
        lock (_gate)
        {
            var parent = ParentDirectory(path.Value);
            if (parent is not null && !_directories.Contains(parent))
            {
                throw new InvalidOperationException(
                    $"Parent directory '{parent}' does not exist; call CreateDirectory first.");
            }

            if (_directories.Contains(path.Value))
            {
                throw new InvalidOperationException($"'{path.Value}' is a directory.");
            }

            _files[path.Value] = [.. content];
        }
    }

    /// <summary>Directly installs UTF-8 text into the virtual tree, bypassing grant validation.</summary>
    /// <param name="path">The path to install, whose parent must already be an existing directory.</param>
    /// <param name="content">The exact text to install; replaces any existing file at <paramref name="path"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The parent directory does not exist, or a directory already occupies <paramref name="path"/>.
    /// </exception>
    public void Seed(FileSystemPath path, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Seed(path, Encoding.UTF8.GetBytes(content));
    }

    /// <summary>Directly creates a directory and every missing ancestor, bypassing grant validation.</summary>
    /// <param name="path">The directory to create.</param>
    /// <exception cref="InvalidOperationException">A file already occupies <paramref name="path"/> or one of its ancestors.</exception>
    public void CreateDirectory(FileSystemPath path)
    {
        lock (_gate)
        {
            var current = "";
            foreach (var segment in path.Value.Split('/'))
            {
                current = current.Length == 0 ? segment : $"{current}/{segment}";
                if (_files.ContainsKey(current))
                {
                    throw new InvalidOperationException($"'{current}' is a file.");
                }

                _ = _directories.Add(current);
            }
        }
    }

    /// <summary>Directly observes exact bytes installed in the virtual tree, bypassing grant validation.</summary>
    /// <param name="path">The path to observe.</param>
    /// <param name="content">The exact installed bytes when a file exists at <paramref name="path"/>.</param>
    /// <returns><see langword="true"/> when a file exists at <paramref name="path"/>.</returns>
    public bool TryReadAllBytes(FileSystemPath path, out ImmutableArray<byte> content)
    {
        lock (_gate)
        {
            return _files.TryGetValue(path.Value, out content);
        }
    }

    /// <inheritdoc/>
    [Obsolete("Use narrow host capability contracts selected through IFileSystemSelector instead.")]
    private async Task<FileReadResult> ReadCoreAsync(LegacyFileReadRequest request, CancellationToken cancellationToken)
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
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return new FileReadDenied(FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        ImmutableArray<byte> content;
        lock (_gate)
        {
            if (!_files.TryGetValue(request.Path.Value, out content))
            {
                return new FileNotFound(request.Path);
            }
        }

        return content.Length > _maximumReadBytes
            ? new FileReadFailed($"File exceeds the configured maximum of {_maximumReadBytes} bytes.")
            : new FileRead(Encoding.UTF8.GetString(content.AsSpan()), content.Length);
    }

    /// <inheritdoc/>
    [Obsolete("Use narrow host capability contracts selected through IFileSystemSelector instead.")]
    private async Task<LegacyFileWriteResult> WriteCoreAsync(FileWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfUndefined(request.Mode);
        cancellationToken.ThrowIfCancellationRequested();

        var contentBytes = Encoding.UTF8.GetByteCount(request.Content);
        if (contentBytes > _maximumWriteBytes)
        {
            return new LegacyFileWriteDenied(
                $"Content is {contentBytes} bytes, exceeding the configured maximum of {_maximumWriteBytes}.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(request.Mode),
            [FileSecurityBinding.Resource(request.Path)],
            FileSecurityBinding.WriteFingerprint(request.Path, request.Content, request.Mode));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return new LegacyFileWriteDenied(FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        lock (_gate)
        {
            var path = request.Path.Value;
            var parent = ParentDirectory(path);
            if ((parent is not null && !_directories.Contains(parent)) || _directories.Contains(path))
            {
                return new LegacyFileWriteFailed("The file could not be written.");
            }

            var exists = _files.TryGetValue(path, out var existing);
            if (request.Mode == FileWriteMode.CreateNew && exists)
            {
                return new LegacyFileAlreadyExists(request.Path);
            }
            if (request.Mode == FileWriteMode.ReplaceExisting && !exists)
            {
                return new LegacyFileWriteFailed("The replacement target does not exist.");
            }
            if (request.Mode == FileWriteMode.Append && !exists)
            {
                return new LegacyFileWriteFailed("The append target does not exist.");
            }

            var bytes = Encoding.UTF8.GetBytes(request.Content);
            _files[path] = request.Mode == FileWriteMode.Append && exists ? existing.AddRange(bytes) : [.. bytes];
            return new LegacyFileWritten(contentBytes);
        }
    }

    /// <summary>Returns the parent directory path, or null when <paramref name="path"/> is a top-level root child.</summary>
    private static string? ParentDirectory(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? null : path[..separator];
    }

    /// <summary>Returns whether a directory exists at <paramref name="path"/>, or always true for the root.</summary>
    private bool DirectoryExists(string? path) => path is null || _directories.Contains(path);
}
