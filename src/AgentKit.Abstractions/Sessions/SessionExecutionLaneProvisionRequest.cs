// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic installation of a previously absent execution lane on one exact branch tip.</summary>
public sealed record SessionExecutionLaneProvisionRequest
{
    /// <summary>Initializes an execution-lane provisioning request.</summary>
    /// <param name="context">The lane-bound before-run context whose lane is being provisioned.</param>
    /// <param name="branchCursor">The exact unowned branch tip the new lane will claim.</param>
    /// <param name="expectedVersion">The observed canonical whole-session version.</param>
    /// <param name="entryId">The reserved identity for the provisioning fact.</param>
    /// <param name="sessionProfile">The selected session profile evidence used for provisioning.</param>
    /// <param name="configuration">The validated configuration evidence used for provisioning.</param>
    /// <param name="provisionedAt">The deterministic commit timestamp.</param>
    /// <param name="idempotencyKey">The exact provisioning idempotency key.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entryId"/> is default.</exception>
    /// <exception cref="ArgumentException">The context is not lane-bound and before-run, configuration differs from authorization, or the idempotency key is blank.</exception>
    public SessionExecutionLaneProvisionRequest(
        SessionOperationContext context,
        SessionBranchCursor branchCursor,
        SessionVersion expectedVersion,
        SessionEntryId entryId,
        SessionProfileReference sessionProfile,
        RunConfigurationReference configuration,
        DateTimeOffset provisionedAt,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        ArgumentException.ThrowIfSessionContextNotBeforeRun(context);
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(entryId, default);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(configuration.ConfigurationVersion, context.Authorization.ConfigurationVersion, nameof(configuration));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        BranchCursor = branchCursor;
        ExpectedVersion = expectedVersion;
        EntryId = entryId;
        SessionProfile = sessionProfile;
        Configuration = configuration;
        ProvisionedAt = provisionedAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the protected lane-bound context.</summary><value>The exact before-run identity and authorization evidence.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the branch tip to claim.</summary><value>An exact cursor on an existing branch not already owned by another lane.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the expected canonical session version.</summary><value>Whole-session optimistic-concurrency evidence checked after idempotency reconciliation.</value>
    public SessionVersion ExpectedVersion { get; }
    /// <summary>Gets the reserved provisioning-entry identity.</summary><value>A non-default globally unique session-entry identity.</value>
    public SessionEntryId EntryId { get; }
    /// <summary>Gets the provisioning session profile evidence.</summary><value>The exact profile selected for this transaction; later validated runs may capture a newer profile.</value>
    public SessionProfileReference SessionProfile { get; }
    /// <summary>Gets the provisioning configuration evidence.</summary><value>The exact configuration selected for this transaction; it does not freeze later runs to this version.</value>
    public RunConfigurationReference Configuration { get; }
    /// <summary>Gets the commit timestamp.</summary><value>The deterministic time retained on the provisioning fact.</value>
    public DateTimeOffset ProvisionedAt { get; }
    /// <summary>Gets the idempotency key.</summary><value>The key reconciled before version, cursor, or collision checks.</value>
    public IdempotencyKey IdempotencyKey { get; }
}
