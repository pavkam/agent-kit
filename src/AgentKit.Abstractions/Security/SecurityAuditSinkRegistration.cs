// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the bounded event support and delivery semantics of one additive security audit sink.</summary>
public sealed record SecurityAuditSinkRegistration
{
    /// <summary>Initializes immutable security-audit sink capabilities.</summary>
    /// <param name="supportedEventKinds">The initialized non-empty unique event kinds this sink can accept.</param>
    /// <param name="delivery">Whether this sink gates a protected transition when applicable.</param>
    /// <param name="providesDurableAcceptance">Whether successful completion proves the record crossed this sink's durable acceptance point.</param>
    /// <exception cref="ArgumentException"><paramref name="supportedEventKinds"/> is default, empty, or contains a duplicate value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An event kind or <paramref name="delivery"/> is undefined.</exception>
    public SecurityAuditSinkRegistration(
        ImmutableArray<SecurityAuditEventKind> supportedEventKinds,
        SecurityAuditDelivery delivery,
        bool providesDurableAcceptance)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(supportedEventKinds);
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(supportedEventKinds, nameof(supportedEventKinds));

        SupportedEventKinds = supportedEventKinds;
        Delivery = delivery;
        ProvidesDurableAcceptance = providesDurableAcceptance;
    }

    /// <summary>Gets the immutable event kinds the sink declares it can persist.</summary>
    public ImmutableArray<SecurityAuditEventKind> SupportedEventKinds { get; }
    /// <summary>Gets whether an applicable delivery gates protected access.</summary>
    public SecurityAuditDelivery Delivery { get; }
    /// <summary>Gets whether successful delivery proves durable acceptance.</summary>
    public bool ProvidesDurableAcceptance { get; }
}
