// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Persists protected plan revisions as typed entries in the selected session coordinator.</summary>
public sealed class SessionPlanStateStore: IPlanStateStore
{
    private const int _pageSize = 1;
    private readonly ISessionCoordinator _sessions;
    private readonly ISecurityGrantStore _grantStore;
    private readonly IIdentifierGenerator<PlanId> _planIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the session-backed plan state boundary.</summary>
    /// <param name="sessions">The selected session coordinator.</param>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="planIds">The replaceable plan identity source.</param>
    /// <param name="entryIds">The replaceable session-entry identity source.</param>
    /// <param name="intentIds">The replaceable enforcement-attempt identity source.</param>
    /// <param name="timeProvider">The deterministic revision clock.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public SessionPlanStateStore(
        ISessionCoordinator sessions,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<PlanId> planIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(planIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _sessions = sessions;
        _grantStore = grantStore;
        _planIds = planIds;
        _entryIds = entryIds;
        _intentIds = intentIds;
        _timeProvider = timeProvider;
    }

    /// <summary>Initializes the session-backed plan boundary with its collision-resistant default intent source.</summary>
    /// <param name="sessions">The selected session coordinator.</param>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="planIds">The replaceable plan identity source.</param>
    /// <param name="entryIds">The replaceable session-entry identity source.</param>
    /// <param name="timeProvider">The deterministic revision clock.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <remarks>Dependency-injection composition uses the full constructor so hosts can replace intent generation. This overload preserves direct construction compatibility.</remarks>
    public SessionPlanStateStore(
        ISessionCoordinator sessions,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<PlanId> planIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider)
        : this(sessions, grantStore, planIds, entryIds, new GuidSecurityEnforcementIntentIdGenerator(), timeProvider)
    {
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.plan.session-store");

    /// <inheritdoc/>
    public async ValueTask<PlanStateResult> ReadAsync(
        PlanReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = request.Context.ToAddress();
        var denied = await ConsumeAsync(
            request.Context,
            request.Grant,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            PlanSecurityBinding.ReadFingerprint(address),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var loaded = await LoadCurrentAsync(request.Context, request.SessionProfile, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is not null
            ? new PlanStateFailed(loaded.Error)
            : loaded.Plan is null
                ? new PlanStateMissing()
                : new PlanStateFound(loaded.Plan);
    }

    /// <inheritdoc/>
    public async ValueTask<PlanStateResult> ReplaceAsync(
        PlanReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = request.Context.ToAddress();
        var denied = await ConsumeAsync(
            request.Context,
            request.Grant,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            PlanSecurityBinding.ReplaceFingerprint(address, request.Title, request.Items, request.ExpectedRevision),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var loaded = await LoadCurrentAsync(request.Context, request.SessionProfile, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
        {
            return new PlanStateFailed(loaded.Error);
        }

        if (request.ExpectedRevision != loaded.Plan?.Revision)
        {
            return new PlanStateConflict(loaded.Plan?.Revision);
        }

        var plan = new WorkPlan(
            loaded.Plan?.Id ?? _planIds.Create(),
            new PlanRevision((loaded.Plan?.Revision.Value ?? 0) + 1),
            request.Title,
            request.Items,
            request.Context.Identity,
            _timeProvider.GetUtcNow());
        return await AppendAsync(
            request.Context,
            request.SessionProfile,
            request.ToolCallId,
            loaded.Descriptor!,
            loaded.LastEntryId,
            plan,
            request.Grant.InputFingerprint,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PlanStateResult> SetStatusAsync(
        PlanStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = request.Context.ToAddress();
        var denied = await ConsumeAsync(
            request.Context,
            request.Grant,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            PlanSecurityBinding.StatusFingerprint(address, request.ItemId, request.Status, request.ExpectedRevision),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var loaded = await LoadCurrentAsync(request.Context, request.SessionProfile, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
        {
            return new PlanStateFailed(loaded.Error);
        }

        if (loaded.Plan is null)
        {
            return new PlanStateMissing();
        }

        if (loaded.Plan.Revision != request.ExpectedRevision)
        {
            return new PlanStateConflict(loaded.Plan.Revision);
        }

        var index = -1;
        for (var candidate = 0; candidate < loaded.Plan.Items.Length; candidate++)
        {
            if (loaded.Plan.Items[candidate].Id == request.ItemId)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0)
        {
            return new PlanStateFailed("The requested plan item does not exist.");
        }

        var items = loaded.Plan.Items.SetItem(index, loaded.Plan.Items[index] with { Status = request.Status });
        try
        {
            var plan = new WorkPlan(
                loaded.Plan.Id,
                new PlanRevision(loaded.Plan.Revision.Value + 1),
                loaded.Plan.Title,
                items,
                request.Context.Identity,
                _timeProvider.GetUtcNow());
            return await AppendAsync(
                request.Context,
                request.SessionProfile,
                request.ToolCallId,
                loaded.Descriptor!,
                loaded.LastEntryId,
                plan,
                request.Grant.InputFingerprint,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return new PlanStateFailed(exception.Message);
        }
    }

    private async ValueTask<PlanStateDenied?> ConsumeAsync(
        SessionOperationContext context,
        SecurityGrant grant,
        SecurityOperationKind kind,
        SecurityEffect effect,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        Debug.Assert(context is not null, "A validated plan request supplies its session operation context.");
        Debug.Assert(grant is not null, "A validated plan request supplies its security grant.");
        Debug.Assert(Enum.IsDefined(kind), "The plan boundary supplies a defined security operation kind.");
        Debug.Assert(Enum.IsDefined(effect), "The plan boundary supplies a defined security effect.");
        Debug.Assert(!string.IsNullOrWhiteSpace(fingerprint.Value),
            "The plan boundary supplies a canonical nonblank input fingerprint.");

        cancellationToken.ThrowIfCancellationRequested();
        var enforcement = new SecurityEnforcementRequest(
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.Identity,
            context.Authorization,
            SecurityAudience,
            kind,
            effect,
            [PlanSecurityBinding.Resource(context.ToAddress())],
            fingerprint,
            grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), requiredFence: null);
        var result = await _grantStore.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Status == GrantConsumptionStatus.Consumed
            && ReceiptMatches(result.IntentReceipt, grant, enforcement, intent)
            ? null
            : new PlanStateDenied(result.SafeMessage);
    }

    private static bool ReceiptMatches(
        SecurityEnforcementIntentReceipt? receipt,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        Debug.Assert(grant is not null, "The public plan request owns a validated grant.");
        Debug.Assert(enforcement is not null, "The plan boundary constructed exact enforcement evidence.");
        Debug.Assert(intent is not null, "The plan boundary constructed an enforcement intent.");

        return receipt is not null
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && HasExactEnforcement(receipt.Enforcement, enforcement);
    }

    private static bool HasExactEnforcement(
        SecurityEnforcementRequest actual,
        SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt retains non-null enforcement evidence.");
        Debug.Assert(expected is not null, "The plan boundary created non-null enforcement evidence.");
        return actual.Scope == expected.Scope
            && actual.Identity == expected.Identity
            && actual.Authorization == expected.Authorization
            && actual.Audience == expected.Audience
            && actual.Kind == expected.Kind
            && actual.Effect == expected.Effect
            && actual.InputFingerprint == expected.InputFingerprint
            && actual.RevocationVersion == expected.RevocationVersion
            && actual.Resources.SequenceEqual(expected.Resources);
    }

    private async ValueTask<(SessionDescriptor? Descriptor, WorkPlan? Plan, SessionEntryId? LastEntryId, string? Error)>
        LoadCurrentAsync(
            SessionOperationContext context,
            SessionProfileSnapshot sessionProfile,
            CancellationToken cancellationToken)
    {
        var load = await _sessions.LoadAsync(context, sessionProfile, cancellationToken).ConfigureAwait(false);
        if (load is not SessionLoaded loaded)
        {
            return (null, null, null, load switch
            {
                SessionNotFound => "The target session does not exist.",
                SessionLoadFailed failed => failed.SafeMessage,
                _ => "The target session returned an unsupported load result.",
            });
        }

        WorkPlan? current = null;
        SessionEntryId? lastEntryId = null;
        var sequence = new SessionSequence(0);
        while (true)
        {
            var pageResult = await _sessions.ReadAsync(
                new SessionReadRequest(context, loaded.Descriptor.ActiveBranchId, sequence, _pageSize),
                sessionProfile,
                cancellationToken).ConfigureAwait(false);
            if (pageResult is not SessionPage page)
            {
                return (loaded.Descriptor, null, null, pageResult is SessionReadFailed failed
                    ? failed.SafeMessage
                    : "The session plan history could not be read.");
            }

            foreach (var entry in page.Entries.OfType<PlanSessionEntry>())
            {
                current = entry.Plan;
                lastEntryId = entry.Id;
            }

            sequence = page.ThroughSequence;
            if (!page.HasMore)
            {
                return (loaded.Descriptor, current, lastEntryId, null);
            }
        }
    }

    private async ValueTask<PlanStateResult> AppendAsync(
        SessionOperationContext context,
        SessionProfileSnapshot sessionProfile,
        ToolCallId toolCallId,
        SessionDescriptor descriptor,
        SessionEntryId? causalParentId,
        WorkPlan plan,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var entry = new PlanSessionEntry(
            _entryIds.Create(),
            descriptor.Address,
            context.Correlation,
            descriptor.ActiveBranchId,
            new SessionSequence(descriptor.Version.Value + 1),
            causalParentId,
            _timeProvider.GetUtcNow(),
            new SchemaVersion("1"),
            plan);
        var append = await _sessions.AppendAsync(
            new SessionAppendRequest(
                context,
                descriptor.ActiveBranchId,
                descriptor.Version,
                new IdempotencyKey($"plan:{toolCallId}:{fingerprint.Value}"),
                [entry]),
            sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (append is SessionAppended)
        {
            return new PlanStateFound(plan);
        }

        if (append is SessionAppendConflict)
        {
            var current = await LoadCurrentAsync(context, sessionProfile, cancellationToken).ConfigureAwait(false);
            return new PlanStateConflict(current.Plan?.Revision);
        }

        return new PlanStateFailed(append switch
        {
            SessionAppendFailed failed => failed.SafeMessage,
            SessionAppendNotFound => "The target session no longer exists.",
            _ => "The plan revision could not be committed.",
        });
    }
}
