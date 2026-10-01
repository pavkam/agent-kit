// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Configures bounded grant issue by the first-party security authority.</summary>
public sealed class AgentPermissionOptions
{
    /// <summary>Gets or sets the published effective policy version.</summary>
    public long PolicyVersion { get; set; } = 1;
    /// <summary>Gets or sets the exact immutable policy snapshot evaluated by this authority.</summary>
    /// <value>The snapshot binding every captured authorization context must carry for this authority to evaluate it. It is an external fact of the composition, so there is no default, and composition validation rejects a null value and a version that differs from <see cref="PolicyVersion"/>.</value>
    /// <remarks>
    /// Every request carries the captured <c>SecurityAuthorizationContext</c> it was selected under, and the authority
    /// denies with code <c>"security.captured_context_mismatch"</c> any request whose captured snapshot is not this one.
    /// Set it to the same <see cref="SecurityPolicySnapshotReference"/> published in the corresponding
    /// <c>SecurityProfilePublication</c>. Prefer <c>AgentKit.Permissions.ServiceExtensions.AddStandaloneSecurityProfile</c>,
    /// which derives and wires a matching snapshot and publication together.
    /// </remarks>
    public SecurityPolicySnapshotReference? PolicySnapshot { get; set; }
    /// <summary>Gets or sets the current revocation epoch.</summary>
    public long RevocationVersion { get; set; } = 1;
    /// <summary>Gets or sets the maximum lifetime of any issued grant.</summary>
    public TimeSpan MaximumGrantLifetime { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Gets or sets the maximum use count of any issued grant.</summary>
    public int MaximumGrantUses { get; set; } = 1;

    /// <summary>Gets or sets how durable approvals behave when no inline handler resolves them.</summary>
    /// <value><see cref="HeadlessApprovalBehavior.Deny"/> unless the host explicitly opts into durable deferral.</value>
    public HeadlessApprovalBehavior HeadlessApprovalBehavior { get; set; } = HeadlessApprovalBehavior.Deny;

    /// <summary>Gets or sets the durability profile the approval-wait recorder journals deferred approval waits under.</summary>
    /// <value>
    /// A nondefault profile key, or <see langword="null"/> — the default — when a deferred approval leaves no durable
    /// operation record.
    /// </value>
    /// <remarks>
    /// <para>
    /// The profile is configured here rather than read from an agent definition because the
    /// <see cref="DurableApprovalWaitRecorder"/> is an engine-wide singleton: it records waits for every hosted agent
    /// and holds no per-agent selection. A host whose agents need different durability profiles for approval waits
    /// replaces the recorder with its own <see cref="IApprovalWaitRecorder"/> instead of expecting this single value
    /// to vary.
    /// </para>
    /// <para>
    /// Setting this key promises evidence will exist, so a wait is journaled only when the composed durability
    /// runtime resolves the profile and that profile enables
    /// <see cref="PermissionsDurableOperations.ApprovalWait"/>. When durability is not composed, the profile is
    /// unknown, or the name is not enabled, the recorder behaves exactly as it does undurably: the decision is still
    /// reported to the caller, and no security decision changes.
    /// </para>
    /// </remarks>
    public DurabilityProfileKey? DurabilityProfile { get; set; }

    /// <summary>Gets or sets the default delivery requirement for security audit records.</summary>
    /// <value><see cref="SecurityAuditDelivery.Required"/> unless the host explicitly accepts best-effort audit export.</value>
    public SecurityAuditDelivery AuditDelivery { get; set; } = SecurityAuditDelivery.Required;

    /// <summary>Gets or sets the finite deadline applied to each audit-sink delivery attempt.</summary>
    /// <value>A positive duration no greater than <see cref="MaximumAuditDeliveryTimeout"/>; the default is thirty seconds.</value>
    /// <remarks>
    /// A deadline ends this dispatch attempt even when a sink ignores cancellation. It cannot prove that a timed-out
    /// sink did not persist the record, so callers receive a typed ambiguous result for required delivery and must not
    /// retry a protected effect merely because audit acceptance became unknown.
    /// </remarks>
    public TimeSpan AuditDeliveryTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the greatest duration supported by the timer APIs used to bound audit delivery.</summary>
    /// <value>The largest positive timeout accepted by <see cref="CancellationTokenSource"/> and task timeout APIs.</value>
    internal static TimeSpan MaximumAuditDeliveryTimeout { get; } = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>Determines whether an audit delivery timeout can be scheduled by the bounded dispatcher.</summary>
    /// <param name="timeout">The candidate delivery deadline.</param>
    /// <returns><see langword="true"/> when the timeout is positive and supported by the underlying timer APIs.</returns>
    internal static bool IsSupportedAuditDeliveryTimeout(TimeSpan timeout) =>
        timeout > TimeSpan.Zero && timeout <= MaximumAuditDeliveryTimeout;
}
