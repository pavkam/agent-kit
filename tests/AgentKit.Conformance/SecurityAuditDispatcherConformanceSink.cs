// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Pairs one public audit-sink capability declaration with the sink instance a conformance fixture composes.</summary>
public sealed record SecurityAuditDispatcherConformanceSink
{
    /// <summary>Initializes one fixture-owned audit-sink binding.</summary>
    /// <param name="registration">The immutable public delivery and durable-acceptance declaration.</param>
    /// <param name="sink">The sink implementation under test.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registration"/> or <paramref name="sink"/> is null.</exception>
    public SecurityAuditDispatcherConformanceSink(SecurityAuditSinkRegistration registration, ISecurityAuditSink sink)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(sink);
        Registration = registration;
        Sink = sink;
    }

    /// <summary>Gets the immutable capabilities that the paired sink declares.</summary>
    public SecurityAuditSinkRegistration Registration { get; }

    /// <summary>Gets the fixture-owned sink instance.</summary>
    public ISecurityAuditSink Sink { get; }
}
