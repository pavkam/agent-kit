// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Persists authoritative tenant-partitioned session routes as a newline-delimited JSON transition log.</summary>
/// <remarks>
/// <para>
/// A routing record decides which store owns a session, so this directory is durable: every committed route is appended and
/// flushed to disk before the in-memory projection changes, and <see cref="InitializeAsync"/> replays the log through the
/// same deterministic commit path. A route pinned before a restart is therefore still pinned after it, and a creation retry
/// issued across a restart still reconciles against its original evidence instead of allocating a second session.
/// </para>
/// <para>
/// Every lookup and mutation requires required-audit acceptance followed by exact grant consumption before directory state
/// is read or changed. The directory masks cross-tenant existence, never probes stores, and never rebinds a pinned
/// location. A host-local advisory exclusive lock is held for the directory's lifetime, so a second writer on the same host
/// fails fast; this is not a distributed lease.
/// </para>
/// <para>
/// The directory owns its root completely, including the manifest and the advisory lock. It must not be pointed at the
/// session store's root.
/// </para>
/// </remarks>
public sealed partial class JsonSessionDirectory: ISessionDirectory, IDisposable
{
    private const string _storeKind = "agentkit.session.directory";
    private const string _logName = "directory";
    private const int _schemaVersion = 1;

    private readonly Lock _gate = new();
    private readonly Dictionary<SessionAddress, SessionLocation> _locations = [];
    private readonly Dictionary<SessionAddress, PrincipalId> _owners = [];
    private readonly Dictionary<CreationRouteKey, CreationRoute> _creationRoutes = [];
    private readonly Dictionary<DirectoryWriteKey, DirectoryWriteRoute> _writeRoutes = [];
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grantStore;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonSessionDirectory> _logger;
    private readonly JsonSessionDirectoryTarget _target;
    private readonly JsonSessionDirectorySettings _settings;
    private readonly JsonEncodingSettings _recordEncoding;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _replaying;
    private bool _disposed;

    /// <summary>Initializes a durable directory for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="securityAudience">The non-default component identity permitted to consume directory grants.</param>
    /// <param name="auditDispatcher">The non-null dispatcher that must accept required audit before access.</param>
    /// <param name="grantStore">The non-null authoritative grant store that validates and consumes one exact directory use.</param>
    /// <param name="auditRecordIds">The non-null deterministic source for audit-record identities.</param>
    /// <param name="timeProvider">The non-null clock used only for audit timestamps.</param>
    /// <param name="target">The exact directory root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable bounds, compaction policy, and encoding contract.</param>
    /// <param name="logger">The optional logger for content-free directory diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">A collaborator, the target, or the settings value is null.</exception>
    /// <remarks>
    /// Construction performs no I/O, so composition never touches the filesystem; every declared effect happens in
    /// <see cref="InitializeAsync"/>. Routing records carry no session entry, so the directory takes no dependency on an
    /// entry-codec catalog and derives its own record contract from the configured encoding.
    /// </remarks>
    public JsonSessionDirectory(
        ComponentId securityAudience,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        JsonSessionDirectoryTarget target,
        JsonSessionDirectorySettings settings,
        ILogger<JsonSessionDirectory>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);

        SecurityAudience = securityAudience;
        _auditDispatcher = auditDispatcher;
        _grantStore = grantStore;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _target = target;
        _settings = settings;
        _logger = logger ?? NullLogger<JsonSessionDirectory>.Instance;
        _recordEncoding = JsonSessionSerialization.CreateDirectoryRecordEncoding(settings.Encoding);
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>: a committed route is flushed to disk before it is observable.</value>
    public bool Durable => true;

    /// <inheritdoc/>
    /// <value>The single component identity permitted to consume grants issued for this durable JSON directory.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Validates or creates the directory root, binds its encoding contract, and replays committed routes.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for routing operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="ObjectDisposedException">The directory was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, directory identity, encoding contract, or persisted evidence cannot be validated safely.</exception>
    /// <exception cref="IOException">The root or its record log cannot be read or written.</exception>
    /// <remarks>Repeating initialization after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (_initialized)
            {
                return ValueTask.CompletedTask;
            }

            _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
            JsonStoreRoot.ValidateFile(_root.ManifestPath);
            JsonStoreRoot.ValidateFile(_log.Path);
            _exclusive = JsonStoreLock.Acquire(_root.LockPath);
            cancellationToken.ThrowIfCancellationRequested();
            JsonStoreSerialization.VerifyRoundTrip(JsonSessionDirectoryProbe.Create(), _recordEncoding.RecordOptions);
            cancellationToken.ThrowIfCancellationRequested();
            BindManifest(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Replay(cancellationToken);
            _initialized = true;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the advisory exclusive lock held for this directory's lifetime and drops the projection.</summary>
    /// <remarks>
    /// Disposal is idempotent and does not flush: every acknowledged route was already flushed when it was committed. A
    /// disposed directory cannot serve further operations.
    /// </remarks>
    public void Dispose()
    {
        using (_gate.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;
            _locations.Clear();
            _owners.Clear();
            _creationRoutes.Clear();
            _writeRoutes.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private SessionLocationResult LocateCore(SessionOperationContext context)
    {
        Debug.Assert(context is not null, "The public boundary validates the operation context.");
        using (_gate.EnterScope())
        {
            var address = context.ToAddress();
            return !_locations.TryGetValue(address, out var location)
                || location.TenantId != context.Identity.TenantId
                ? new SessionLocationNotFound(address)
                : new SessionLocated(location);
        }
    }

    private SessionCreationLocationResult LocateForCreateCore(SessionCreateRequest create)
    {
        Debug.Assert(create is not null, "The public boundary validates the creation request.");
        using (_gate.EnterScope())
        {
            var key = new CreationRouteKey(create.Identity.TenantId, create.AgentId, create.IdempotencyKey);
            return !_creationRoutes.TryGetValue(key, out var route)
                ? new SessionCreationLocationNotFound()
                : route.Request.Equals(create)
                ? new SessionCreationLocationLocated(route.Location)
                : new SessionCreationLocationConflict(
                    "The creation retry key was already used with different request evidence.");
        }
    }

    private SessionDirectoryWriteResult RecordCore(
        SessionDirectoryWriteRequest write, CancellationToken cancellationToken)
    {
        Debug.Assert(write is not null, "The public boundary validates the write request.");
        using (_gate.EnterScope())
        {
            var routeKey = new DirectoryWriteKey(
                write.Context.Identity.TenantId, write.Context.ToAddress(), write.IdempotencyKey);
            if (_writeRoutes.TryGetValue(routeKey, out var previousWrite))
            {
                return previousWrite.Request.Equals(write)
                    ? new SessionLocationRecorded(previousWrite.Location, existing: true)
                    : new SessionLocationConflict(previousWrite.Location, write.Location.StoreKey);
            }

            if (!_locations.TryGetValue(write.Location.Address, out var existing))
            {
                Persist(JsonSessionDirectoryLogRecord.ForWrite(write), cancellationToken);
                _locations.Add(write.Location.Address, write.Location);
                _owners.Add(write.Location.Address, write.Context.Identity.PrincipalId);
                _writeRoutes.Add(routeKey, new DirectoryWriteRoute(write, write.Location));
                return new SessionLocationRecorded(write.Location, existing: false);
            }

            if (existing.TenantId != write.Context.Identity.TenantId)
            {
                return new SessionDirectoryWriteDenied("The directory route cannot be recorded.");
            }
            if (!_owners.TryGetValue(existing.Address, out var owner)
                || owner != write.Context.Identity.PrincipalId)
            {
                return new SessionDirectoryWriteDenied("The directory route cannot be recorded.");
            }
            if (existing.StoreKey != write.Location.StoreKey)
            {
                return new SessionLocationConflict(existing, write.Location.StoreKey);
            }

            Persist(JsonSessionDirectoryLogRecord.ForWrite(write), cancellationToken);
            _writeRoutes.Add(routeKey, new DirectoryWriteRoute(write, existing));
            return new SessionLocationRecorded(existing, existing: true);
        }
    }

    private SessionDirectoryWriteResult RecordCreateCore(
        SessionDirectoryCreateRecordRequest record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "The public boundary validates the create-record request.");
        using (_gate.EnterScope())
        {
            var create = record.Request;
            var routeKey = new CreationRouteKey(create.Identity.TenantId, create.AgentId, create.IdempotencyKey);
            if (_creationRoutes.TryGetValue(routeKey, out var existingRoute))
            {
                return existingRoute.Request.Equals(create)
                    ? new SessionLocationRecorded(existingRoute.Location, existing: true)
                    : new SessionLocationConflict(existingRoute.Location, record.Location.StoreKey);
            }

            if (_locations.TryGetValue(record.Location.Address, out var existingLocation))
            {
                return existingLocation.TenantId != create.Identity.TenantId
                    ? new SessionDirectoryWriteDenied("The directory route cannot be recorded.")
                    : new SessionLocationConflict(existingLocation, record.Location.StoreKey);
            }

            Persist(JsonSessionDirectoryLogRecord.ForCreate(record), cancellationToken);
            _locations.Add(record.Location.Address, record.Location);
            _owners.Add(record.Location.Address, create.Identity.PrincipalId);
            _creationRoutes.Add(routeKey, new CreationRoute(create, record.Location));
            return new SessionLocationRecorded(record.Location, existing: false);
        }
    }

    private SessionDirectoryPage ListCore(SessionDirectoryListRequest scan)
    {
        Debug.Assert(scan is not null, "The public boundary validates the list request.");
        using (_gate.EnterScope())
        {
            var ordered = _locations.Values
                .Where(location => location.TenantId == scan.Identity.TenantId
                    && location.Address.AgentId == scan.AgentId
                    && _owners.TryGetValue(location.Address, out var owner)
                    && owner == scan.Identity.PrincipalId
                    && (scan.AfterSessionId is null
                        || location.Address.SessionId.Value.CompareTo(scan.AfterSessionId.Value.Value) > 0))
                .OrderBy(static location => location.Address.SessionId.Value)
                .Take(scan.MaximumResults + 1)
                .ToArray();
            var hasMore = ordered.Length > scan.MaximumResults;
            var page = ordered.Take(scan.MaximumResults).ToImmutableArray();
            var next = hasMore ? page[^1].Address.SessionId : (SessionId?) null;
            return new SessionDirectoryPage(page, next);
        }
    }
}
