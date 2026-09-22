// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

using AgentKit.Permissions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>Persists authoritative security grants and enforcement receipts as a newline-delimited JSON transition log.</summary>
/// <remarks>
/// <para>
/// The store is log structured. Every state change is one JSON line appended and flushed to disk before the operation is
/// acknowledged, so an acknowledged transition survives process loss. A consumption that produces an enforcement receipt
/// writes the new remaining-use count and the receipt in the same line, making the pair atomic by construction.
/// </para>
/// <para>
/// Live state is projected into memory during trusted bootstrap initialization and kept authoritative under one in-process
/// gate, so concurrent callers cannot exceed <see cref="SecurityGrant.AllowedUses"/>. A host-local advisory exclusive lock
/// is held for the store's lifetime, so a second writer on the same host fails fast instead of interleaving appends. This is
/// durable single-process host-local storage: it provides no distributed lease, no fencing token, and no atomicity with any
/// external effect.
/// </para>
/// <para>
/// Call bootstrap initialization exactly once during trusted bootstrap before resolving the store for use. Enforcement
/// receipts are retained indefinitely so an uncertain attempt can always be reconciled rather than repeated.
/// </para>
/// </remarks>
public sealed partial class JsonSecurityGrantStore: ISecurityGrantStore, IDisposable
{
    private const string _storeKind = "agentkit.permissions.grants";
    private const string _logName = "grants";
    private const int _schemaVersion = 1;
    private readonly JsonSecurityGrantStoreTarget _target;
    private readonly JsonSecurityGrantStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonSecurityGrantStore> _logger;
    private readonly ISecurityAuditDispatcher? _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId>? _auditRecordIds;
    private readonly IOptions<AgentPermissionOptions>? _permissionOptions;
    private readonly SecurityControlPlaneStoreGate _controlPlaneGate = new();
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<GrantId, GrantState> _grants = [];
    private readonly Dictionary<SecurityEnforcementIntentId, SecurityEnforcementIntentReceipt> _receipts = [];
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable evidence bounds, compaction policy, and encoding contract.</param>
    /// <param name="timeProvider">The clock used for validity checks and consumption receipts.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <remarks>Construction performs no I/O, so composition never touches the filesystem; every effect happens during bootstrap initialization.</remarks>
    public JsonSecurityGrantStore(
        JsonSecurityGrantStoreTarget target,
        JsonSecurityGrantStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<JsonSecurityGrantStore>? logger = null)
        : this(target, settings, timeProvider, logger, null, null, null)
    {
    }

    /// <summary>Initializes a store with optional grant-lifecycle audit dispatch.</summary>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable evidence bounds, compaction policy, and encoding contract.</param>
    /// <param name="timeProvider">The clock used for validity checks and consumption receipts.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <param name="auditDispatcher">The optional security-audit dispatcher.</param>
    /// <param name="auditRecordIds">The optional audit-record identity generator.</param>
    /// <param name="permissionOptions">The optional permission options governing audit delivery.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public JsonSecurityGrantStore(
        JsonSecurityGrantStoreTarget target,
        JsonSecurityGrantStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<JsonSecurityGrantStore>? logger,
        ISecurityAuditDispatcher? auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId>? auditRecordIds,
        IOptions<AgentPermissionOptions>? permissionOptions)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<JsonSecurityGrantStore>.Instance;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _permissionOptions = permissionOptions;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays live state into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for grant operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The root, manifest, store identity, encoding contract, or persisted evidence cannot be validated safely.</exception>
    /// <remarks>
    /// <para>
    /// Initialization acquires the advisory exclusive lock first, so a concurrent writer is rejected before any validation
    /// observes a racing state. It then verifies the manifest's store identity, schema version, and encoding fingerprint,
    /// and performs a round-trip self-check proving the configured contract can reproduce this store's evidence.
    /// </para>
    /// <para>
    /// A log ending in an incomplete append is discarded only under <see cref="JsonStoreRecoveryMode.RecoverTornAppends"/>;
    /// under <see cref="JsonStoreRecoveryMode.ValidateExact"/> it is reported as corrupt evidence. Repeating initialization
    /// after success is a no-op.
    /// </para>
    /// </remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ExecuteVoidAsync(
        "initialize",
        () => InitializeSchemaCore(cancellationToken));

    /// <summary>Records bootstrap evidence and initializes the JSON grant store under host authorization.</summary>
    /// <param name="bootstrap">The bounded bootstrap capability evidence supplied by the host.</param>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed after bootstrap evidence is recorded and the root is ready.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bootstrap"/> is null.</exception>
    public async ValueTask InitializeAsync(
        SecurityControlPlaneBootstrap bootstrap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        _controlPlaneGate.CompleteBootstrap(bootstrap);
    }

    private void InitializeSchemaCore(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
            JsonStoreRoot.ValidateFile(_root.ManifestPath);
            JsonStoreRoot.ValidateFile(_log.Path);
            _exclusive = JsonStoreLock.Acquire(_root.LockPath);
            cancellationToken.ThrowIfCancellationRequested();
            JsonStoreSerialization.VerifyRoundTrip(
                JsonSecurityGrantStoreProbe.Create(), _settings.Encoding.RecordOptions);
            cancellationToken.ThrowIfCancellationRequested();
            BindManifest(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Replay(cancellationToken);
            _initialized = true;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the registration append begins.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <remarks>
    /// Registering the exact same immutable grant again is idempotent and appends nothing. A different grant under an
    /// existing identity is a conflict and leaves the log untouched. Because the record is flushed before acknowledgement,
    /// a failure reported to the caller means the grant is either absent or byte-identical to the retried evidence.
    /// </remarks>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateGrant(grant);
        return ExecuteVoidAsync("register", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
                if (_grants.TryGetValue(grant.Id, out var existing))
                {
                    if (existing.Grant != grant)
                    {
                        throw new InvalidOperationException(
                            "The grant identifier is already registered with different evidence.");
                    }

                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AppendRecord(JsonSecurityGrantLogRecord.ForRegistration(grant), cancellationToken);
                _grants.Add(grant.Id, new GrantState(grant, grant.AllowedUses, false));
            }
        }, grant);
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the consumption append begins.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <remarks>This receiptless path retains no attempt evidence, so an uncertain acknowledgement cannot later be proven to have consumed a use.</remarks>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(grant, enforcement);
        return ConsumeWithLifecycleAuditAsync(grant, enforcement, null, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the consumption append begins.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <remarks>
    /// The remaining-use decrement and the receipt are written as one line, so recovery never observes a consumed use
    /// without its receipt. Presenting the same intent again returns <see cref="GrantConsumptionStatus.Reconciled"/> with
    /// the historical receipt, which is evidence of the earlier attempt and never fresh authority to repeat the effect.
    /// </remarks>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(grant, enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        return ConsumeWithLifecycleAuditAsync(grant, enforcement, intent, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the revocation append begins.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <remarks>Revocation is idempotent; revoking an already revoked grant appends nothing and still reports that the grant exists.</remarks>
    public async ValueTask<GrantRevocationResult> RevokeAsync(
        GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentOutOfRangeException.ThrowIfEqual(grantId, default);
        SecurityGrant? revokedGrant = null;
        var result = await ExecuteAsync(
            "revoke",
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    RequireInitialized();
                    if (!_grants.TryGetValue(grantId, out var state))
                    {
                        return (GrantRevocationResult) new GrantRevocationNotFound(grantId);
                    }
                    if (state.Revoked)
                    {
                        return new GrantAlreadyRevoked(grantId);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    AppendRecord(JsonSecurityGrantLogRecord.ForRevocation(grantId), cancellationToken);
                    _grants[grantId] = state with { Revoked = true };
                    revokedGrant = state.Grant;
                    return new GrantRevoked(grantId, reason);
                }
            }, null, null, grantId).ConfigureAwait(false);
        if (result is GrantRevoked && revokedGrant is not null)
        {
            await EmitGrantLifecycleAsync(
                revokedGrant,
                SecurityAuditOutcome.Accepted,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Releases the advisory exclusive lock held for this store's lifetime.</summary>
    /// <remarks>
    /// Disposal is idempotent and does not flush: every acknowledged record was already flushed to disk when it was
    /// appended. Projected state is dropped, so a disposed store cannot serve further operations.
    /// </remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;
            _grants.Clear();
            _receipts.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private async ValueTask<GrantConsumptionResult> ConsumeWithLifecycleAuditAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent? intent,
        CancellationToken cancellationToken)
    {
        var preview = await ExecuteAsync(
            intent is null ? "consume_assess" : "consume_intent_assess",
            () => PreviewConsumption(grant, enforcement, intent, cancellationToken),
            grant,
            intent).ConfigureAwait(false);
        if (preview.Status is GrantConsumptionStatus.Revoked or GrantConsumptionStatus.Expired)
        {
            await EmitGrantLifecycleAsync(
                grant,
                SecurityAuditOutcome.Denied,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            return preview;
        }

        if (preview.Status is GrantConsumptionStatus.Consumed)
        {
            if (await RefuseConsumptionAuditAsync(grant, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
            {
                return Result(GrantConsumptionStatus.Unknown, 0, "Required grant lifecycle audit was not accepted.");
            }
        }

        if (preview.Status is not GrantConsumptionStatus.Consumed)
        {
            return preview;
        }

        var committed = await ExecuteAsync(
            intent is null ? "consume" : "consume_intent",
            () => CommitConsumption(grant, enforcement, intent, cancellationToken),
            grant,
            intent).ConfigureAwait(false);
        if (committed.Status is GrantConsumptionStatus.Consumed)
        {
            await EmitGrantLifecycleAsync(
                grant,
                SecurityAuditOutcome.Accepted,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }

        return committed;
    }

    private GrantConsumptionResult PreviewConsumption(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent? intent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(grant is not null, "Caller-validated grant evidence is required.");
        Debug.Assert(enforcement is not null, "Caller-validated enforcement evidence is required.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireInitialized();
            if (!_grants.TryGetValue(grant.Id, out var state))
            {
                return Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown.");
            }
            if (state.Grant != grant)
            {
                return Result(GrantConsumptionStatus.Tampered, state.RemainingUses,
                    "The security grant evidence does not match its authoritative record.");
            }

            ContentHash? fingerprint = intent is null
                ? null
                : SecurityEnforcementBinding.Fingerprint(enforcement, intent);
            if (intent is not null && _receipts.TryGetValue(intent.Id, out var historical))
            {
                Debug.Assert(fingerprint.HasValue, "An intent always has a computed effect fingerprint.");
                return ReceiptMatches(historical, grant, enforcement, intent, fingerprint.GetValueOrDefault())
                    ? Result(GrantConsumptionStatus.Reconciled, state.RemainingUses,
                        "The enforcement intent was reconciled without granting another effect.", historical)
                    : Result(GrantConsumptionStatus.Mismatch, state.RemainingUses,
                        "The enforcement intent identity was reused with different evidence.");
            }

            if (state.Revoked || enforcement.RevocationVersion != grant.RevocationVersion)
            {
                return Result(GrantConsumptionStatus.Revoked, state.RemainingUses,
                    "The security grant is revoked or stale.");
            }

            var now = _timeProvider.GetUtcNow();
            return now < grant.NotBefore || now >= grant.ExpiresAt
                ? Result(GrantConsumptionStatus.Expired, state.RemainingUses,
                    "The security grant is outside its validity window.")
                : !EnforcementMatches(grant, enforcement)
                ? Result(GrantConsumptionStatus.Mismatch, state.RemainingUses,
                    "The concrete effect does not match the security grant.")
                : state.RemainingUses == 0
                    ? Result(GrantConsumptionStatus.Exhausted, 0, "The security grant has no remaining uses.")
                    : Result(
                        GrantConsumptionStatus.Consumed,
                        state.RemainingUses,
                        "The security grant is ready for consumption.");
        }
    }

    private GrantConsumptionResult CommitConsumption(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent? intent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(grant is not null, "Caller-validated grant evidence is required.");
        Debug.Assert(enforcement is not null, "Caller-validated enforcement evidence is required.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireInitialized();
            var result = PreviewConsumption(grant, enforcement, intent, cancellationToken);
            if (result.Status is not GrantConsumptionStatus.Consumed)
            {
                return result;
            }

            if (!_grants.TryGetValue(grant.Id, out var state))
            {
                return Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown.");
            }

            ContentHash? fingerprint = intent is null
                ? null
                : SecurityEnforcementBinding.Fingerprint(enforcement, intent);
            var now = _timeProvider.GetUtcNow();
            var remainingUses = state.RemainingUses - 1;
            var receipt = intent is null
                ? null
                : new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement,
                    intent.RequiredFence, fingerprint!.Value, now);
            cancellationToken.ThrowIfCancellationRequested();
            AppendRecord(JsonSecurityGrantLogRecord.ForConsumption(grant.Id, remainingUses, receipt), cancellationToken);
            _grants[grant.Id] = state with { RemainingUses = remainingUses };
            if (receipt is not null)
            {
                _receipts[receipt.IntentId] = receipt;
            }

            return Result(GrantConsumptionStatus.Consumed, remainingUses,
                intent is null
                    ? "The security grant was consumed."
                    : "The security grant and enforcement intent were consumed.",
                receipt);
        }
    }

    private ValueTask<bool> RefuseConsumptionAuditAsync(
        SecurityGrant grant,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        SecurityGrantStoreLifecycleAudit.RefuseConsumptionIfRequiredAuditFailedAsync(
            _auditDispatcher,
            _auditRecordIds,
            _permissionOptions,
            grant,
            SecurityAuditOutcome.Accepted,
            occurredAt,
            cancellationToken);

    private async ValueTask EmitGrantLifecycleAsync(
        SecurityGrant grant,
        SecurityAuditOutcome outcome,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        _ = await SecurityGrantStoreLifecycleAudit.RefuseConsumptionIfRequiredAuditFailedAsync(
            _auditDispatcher,
            _auditRecordIds,
            _permissionOptions,
            grant,
            outcome,
            occurredAt,
            cancellationToken).ConfigureAwait(false);
    }

    private void BindManifest(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "The advisory exclusive lock is acquired before manifest binding.");
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                    "The configured JSON grant-store root has no manifest.");
            }

            var created = new JsonStoreManifest(_target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion,
                _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(_root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedStoreInstanceId.Value
            || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The JSON grant-store identity does not match bootstrap configuration.");
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                "The JSON grant-store schema version is unsupported.");
        }
        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                "The JSON grant-store root was written under a different encoding contract.");
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_grants.Count == 0, "Replay populates an empty projection.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The JSON grant-store log ends with an incomplete record.");
        }

        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apply(JsonStoreSerialization.Decode<JsonSecurityGrantLogRecord>(
                record.Span, _settings.Encoding.RecordOptions));
        }

        if (replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold)
        {
            Compact(cancellationToken);
        }
    }

    private void Apply(JsonSecurityGrantLogRecord record)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        switch (record.Kind)
        {
            case JsonSecurityGrantLogRecordKind.Registered:
                {
                    var grant = Require(record.Grant, "registration").ToDomain();
                    _grants[grant.Id] = new GrantState(grant, grant.AllowedUses, false);
                    return;
                }
            case JsonSecurityGrantLogRecordKind.Consumed:
                {
                    var state = RequireState(record.GrantId, "consumption");
                    _grants[state.Grant.Id] = state with
                    {
                        RemainingUses = RequireUses(record.RemainingUses, state.Grant.AllowedUses),
                    };
                    if (record.Receipt is { } receipt)
                    {
                        var value = receipt.ToDomain();
                        _receipts[value.IntentId] = value;
                    }

                    return;
                }
            case JsonSecurityGrantLogRecordKind.Revoked:
                {
                    var state = RequireState(record.GrantId, "revocation");
                    _grants[state.Grant.Id] = state with { Revoked = true };
                    return;
                }
            case JsonSecurityGrantLogRecordKind.State:
                {
                    var state = RequireState(record.GrantId, "state");
                    _grants[state.Grant.Id] = state with
                    {
                        RemainingUses = RequireUses(record.RemainingUses, state.Grant.AllowedUses),
                        Revoked = record.Revoked ?? throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                            "A persisted state record omits its revocation flag."),
                    };
                    return;
                }
            case JsonSecurityGrantLogRecordKind.Receipt:
                {
                    var value = Require(record.Receipt, "receipt").ToDomain();
                    _receipts[value.IntentId] = value;
                    return;
                }
            default:
                throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                    "A persisted grant-store record has an unsupported kind.");
        }
    }

    private void Compact(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var records = new List<byte[]>(_grants.Count + _receipts.Count);
        foreach (var state in _grants.Values)
        {
            records.Add(EncodeRecord(JsonSecurityGrantLogRecord.ForRegistration(state.Grant)));
            if (state.RemainingUses != state.Grant.AllowedUses || state.Revoked)
            {
                records.Add(EncodeRecord(
                    JsonSecurityGrantLogRecord.ForState(state.Grant.Id, state.RemainingUses, state.Revoked)));
            }
        }

        foreach (var receipt in _receipts.Values)
        {
            records.Add(EncodeRecord(JsonSecurityGrantLogRecord.ForReceipt(receipt)));
        }

        _log.Compact(records, cancellationToken);
    }

    private void AppendRecord(JsonSecurityGrantLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A record is required.");
        _log.Append(EncodeRecord(record), cancellationToken);
    }

    private byte[] EncodeRecord(JsonSecurityGrantLogRecord record)
    {
        Debug.Assert(record is not null, "A record is required.");
        return JsonStoreSerialization.Encode(record, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
    }

    private void RequireInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "The JSON grant store was used before trusted bootstrap initialization.");
        }

        _controlPlaneGate.RequireReadyForWrites();
    }

    private GrantState RequireState(Guid? grantId, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return grantId is { } value && _grants.TryGetValue(new GrantId(value), out var state)
            ? state
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                $"A persisted {context} record references an unknown grant.");
    }

    private static int RequireUses(int? remainingUses, int allowedUses)
    {
        Debug.Assert(allowedUses > 0, "A registered grant always permits at least one use.");
        return remainingUses is { } value && value >= 0 && value <= allowedUses
            ? value
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "A persisted record violates its grant's authoritative use bounds.");
    }

    private static T Require<T>(T? value, string context)
        where T : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
            $"A persisted {context} record omits its required payload.");
    }

    private static GrantConsumptionResult Result(
        GrantConsumptionStatus status,
        int remainingUses,
        string reason,
        SecurityEnforcementIntentReceipt? receipt = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(reason), "A bounded terminal reason is required.");
        Debug.Assert(remainingUses >= 0, "Remaining uses are never negative.");
        return receipt is null
            ? new GrantConsumptionResult(status, remainingUses, reason)
            : new GrantConsumptionResult(status, remainingUses, reason, receipt);
    }

    private static bool EnforcementMatches(SecurityGrant grant, SecurityEnforcementRequest enforcement) =>
        grant.Scope == enforcement.Scope
        && grant.Identity == enforcement.Identity
        && grant.Authorization == enforcement.Authorization
        && grant.Audience == enforcement.Audience
        && grant.Kind == enforcement.Kind
        && grant.Effect == enforcement.Effect
        && grant.Resources.SequenceEqual(enforcement.Resources)
        && grant.InputFingerprint == enforcement.InputFingerprint;

    private static bool ReceiptMatches(
        SecurityEnforcementIntentReceipt receipt,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        ContentHash fingerprint)
    {
        Debug.Assert(receipt is not null, "Authoritative receipt evidence is required.");
        Debug.Assert(grant is not null, "Presented grant evidence is required.");
        Debug.Assert(enforcement is not null, "Presented enforcement evidence is required.");
        Debug.Assert(intent is not null, "Presented intent evidence is required.");
        return receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.Enforcement == enforcement
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == fingerprint;
    }

    private static void ValidateArguments(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
    }

    private static void ValidateGrant(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant.Scope);
        ArgumentNullException.ThrowIfNull(grant.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(grant.Resources);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(grant.ExpiresAt, grant.NotBefore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grant.AllowedUses);
    }

    private static SecurityGrantStoreUnavailableException Unavailable(
        SecurityGrantStoreFailureKind kind, string message, Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        return new SecurityGrantStoreUnavailableException(kind, message, inner);
    }

    private sealed record GrantState(SecurityGrant Grant, int RemainingUses, bool Revoked);
}
