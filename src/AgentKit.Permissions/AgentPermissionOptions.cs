// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Configures bounded grant issue by the first-party security authority.</summary>
public sealed class AgentPermissionOptions
{
    /// <summary>Gets or sets the published effective policy version.</summary>
    public long PolicyVersion { get; set; } = 1;
    /// <summary>Gets or sets the current revocation epoch.</summary>
    public long RevocationVersion { get; set; } = 1;
    /// <summary>Gets or sets the maximum lifetime of any issued grant.</summary>
    public TimeSpan MaximumGrantLifetime { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>Gets or sets the maximum use count of any issued grant.</summary>
    public int MaximumGrantUses { get; set; } = 1;

    /// <summary>Gets or sets the default delivery requirement for security audit records.</summary>
    /// <value><see cref="SecurityAuditDelivery.Required"/> unless the host explicitly accepts best-effort audit export.</value>
    public SecurityAuditDelivery AuditDelivery { get; set; } = SecurityAuditDelivery.Required;
}
