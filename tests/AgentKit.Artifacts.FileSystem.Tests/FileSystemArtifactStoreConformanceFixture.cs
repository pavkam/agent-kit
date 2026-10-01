// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

using AgentKit.FileSystem.InMemory;

using Microsoft.Extensions.Options;

/// <summary>Composes an isolated file-system artifact store over an in-memory volume, a grant-issuing authority, and the shared deterministic grant store.</summary>
public sealed class FileSystemArtifactStoreConformanceFixture: ArtifactStoreConformanceFixtureBase
{
    private static readonly FileSystemProfileKey _profile = new("artifacts");
    private readonly string _hostRoot = Path.Combine(Path.GetTempPath(), "agentkit-artifacts-fs-" + Guid.NewGuid().ToString("N"));
    private FileSystemArtifactStore? _current;

    /// <summary>Initializes the volume, selector, and authority, none of which perform an effect until the store does.</summary>
    public FileSystemArtifactStoreConformanceFixture()
    {
        Volume = new InMemoryFileSystem(
            Options.Create(new InMemoryFileSystemOptions()),
            Grants,
            Clock,
            logger: null,
            new SequentialIntentIds(),
            new AcceptingAudit(),
            new SequentialAuditIds(),
            _profile);
        Selector = new StaticFileSystemSelector(_profile, Volume, Volume, Volume, Volume);
        Authority = new GrantIssuingAuthority(Grants, Clock);
    }

    /// <summary>Gets the in-memory volume the store writes through.</summary>
    internal InMemoryFileSystem Volume { get; }

    /// <summary>Gets the selector that resolves the volume's reader, writer, deleter, and directory reader.</summary>
    internal StaticFileSystemSelector Selector { get; }

    /// <summary>Gets the authority that authorizes each file effect.</summary>
    internal GrantIssuingAuthority Authority { get; }

    /// <summary>Gets the authoritative grant store shared with every store this fixture opens.</summary>
    internal Permissions.InMemory.InMemorySecurityGrantStore GrantStore => Grants;

    /// <summary>Gets the deterministic clock shared with every store this fixture opens.</summary>
    internal TimeProvider ClockProvider => Clock;

    /// <summary>Gets the target the store writes under.</summary>
    internal FileSystemArtifactTarget Target => new(_profile, new FileRootId("artifacts"), _hostRoot);

    /// <summary>Closes the current store and opens a fresh one over the same volume, sharing no in-process state.</summary>
    /// <returns>The reopened store.</returns>
    internal FileSystemArtifactStore Reopen()
    {
        _current?.Dispose();
        _current = NewStore(Target, FileSystemArtifactSettings.CreateDefault());
        return _current;
    }

    /// <summary>Creates a store over this fixture's volume with explicit target and settings.</summary>
    /// <param name="target">The target.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The store, owned by the caller.</returns>
    internal FileSystemArtifactStore NewStore(FileSystemArtifactTarget target, FileSystemArtifactSettings settings) => new(
        target, settings, Selector, new FixedSecurityAuthoritySelector(Authority),
        new SequentialRequestIds(), new SequentialFileOperationIds(), Grants, new SequentialIntentIds(), Clock);

    /// <inheritdoc/>
    protected override IArtifactStore CreateStore() => _current = NewStore(Target, FileSystemArtifactSettings.CreateDefault());

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        _current?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class AcceptingAudit: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private sealed class SequentialAuditIds: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private long _next;

        public SecurityAuditRecordId Create() => new(Id(Interlocked.Increment(ref _next), 0x41));
    }

    private sealed class SequentialRequestIds: IIdentifierGenerator<SecurityRequestId>
    {
        private long _next;

        public SecurityRequestId Create() => new(Id(Interlocked.Increment(ref _next), 0x42));
    }

    private sealed class SequentialFileOperationIds: IIdentifierGenerator<FileOperationId>
    {
        private long _next;

        public FileOperationId Create() => new(Id(Interlocked.Increment(ref _next), 0x43));
    }

    private static Guid Id(long value, byte marker)
    {
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = marker;
        return new Guid(bytes);
    }
}
