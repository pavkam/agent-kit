// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Configures bounded grant issue by the first-party security authority.</summary>
public sealed class AgentPermissionOptions
{
    /// <summary>Gets or sets the published effective policy version.</summary>
    public long PolicyVersion { get; set; } = 1;
    /// <summary>Gets or sets the exact immutable policy snapshot evaluated by this authority.</summary>
    /// <value>The captured snapshot binding accepted for snapshot-bound protected work, or null when this authority supports only legacy uncaptured requests.</value>
    public SecurityPolicySnapshotReference? PolicySnapshot { get; set; }
    /// <summary>Gets or sets the current revocation epoch.</summary>
    public long RevocationVersion { get; set; } = 1;
    /// <summary>Gets or sets the maximum lifetime of any issued grant.</summary>
    public TimeSpan MaximumGrantLifetime { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Gets or sets the maximum use count of any issued grant.</summary>
    public int MaximumGrantUses { get; set; } = 1;

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
