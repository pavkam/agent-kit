// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Binds one host-owned audit sink to immutable delivery capabilities captured during composition.</summary>
internal sealed record SecurityAuditSinkBinding
{
    /// <summary>Initializes one additive host-owned audit sink binding.</summary>
    /// <param name="registration">The immutable supported-event and delivery declaration.</param>
    /// <param name="sink">The host-owned sink instance.</param>
    /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
    public SecurityAuditSinkBinding(SecurityAuditSinkRegistration registration, ISecurityAuditSink sink)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(sink);
        Registration = registration;
        Sink = sink;
    }

    /// <summary>Gets the captured immutable capabilities for <see cref="Sink"/>.</summary>
    public SecurityAuditSinkRegistration Registration { get; }
    /// <summary>Gets the host-owned sink instance.</summary>
    public ISecurityAuditSink Sink { get; }
}
