// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

/// <summary>Represents a closed set of redacted or intrinsically safe structural audit-field values.</summary>
/// <remarks>The type has no raw-text constructor. Its fingerprint factory computes an SHA-256 digest over supplied hash text, while its typed factories retain only declared security facts, so redaction failure cannot fall back to protected content.</remarks>
public sealed record RedactedAuditValue
{
    private RedactedAuditValue(SecurityAuditValueKind kind, string value)
    {
        Debug.Assert(Enum.IsDefined(kind), "Audit values must use a defined closed value kind.");
        Debug.Assert(!string.IsNullOrWhiteSpace(value), "Audit values must retain a non-empty safe representation.");
        Kind = kind;
        Value = value;
    }

    /// <summary>Creates a redacted SHA-256 value from a supplied non-empty fingerprint representation.</summary>
    /// <param name="fingerprint">The source fingerprint text to hash before storing it as audit evidence.</param>
    /// <returns>A closed, prefixed SHA-256 value that contains no supplied fingerprint text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fingerprint"/> is default.</exception>
    public static RedactedAuditValue FromFingerprint(ContentHash fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint.Value, nameof(fingerprint));
        var bytes = Encoding.UTF8.GetBytes(fingerprint.Value);
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new RedactedAuditValue(SecurityAuditValueKind.Fingerprint, $"sha256:{digest}");
    }

    /// <summary>Creates a safe structural value from a declared policy identifier.</summary>
    /// <param name="policyId">The non-empty policy identifier selected by evaluation.</param>
    /// <returns>A closed audit value retaining the typed policy fact.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policyId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="policyId"/> is blank.</exception>
    public static RedactedAuditValue FromPolicyId(SecurityPolicyId policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId.Value, nameof(policyId));
        return new RedactedAuditValue(SecurityAuditValueKind.SecurityPolicyId, policyId.Value);
    }

    /// <summary>Creates a safe structural value from a declared effecting component identifier.</summary>
    /// <param name="componentId">The non-empty component identifier that enforced the effect.</param>
    /// <returns>A closed audit value retaining the typed component fact.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="componentId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> is blank.</exception>
    public static RedactedAuditValue FromComponentId(ComponentId componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId.Value, nameof(componentId));
        return new RedactedAuditValue(SecurityAuditValueKind.ComponentId, componentId.Value);
    }

    /// <summary>Creates a safe structural value from a defined operation kind.</summary>
    /// <param name="kind">The bounded operation classification.</param>
    /// <returns>A closed audit value retaining the enum name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public static RedactedAuditValue FromOperationKind(SecurityOperationKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        return new RedactedAuditValue(SecurityAuditValueKind.OperationKind, kind.ToString());
    }

    /// <summary>Creates a safe structural value from a defined material effect.</summary>
    /// <param name="effect">The bounded effect classification.</param>
    /// <returns>A closed audit value retaining the enum name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effect"/> is undefined.</exception>
    public static RedactedAuditValue FromEffect(SecurityEffect effect)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        return new RedactedAuditValue(SecurityAuditValueKind.Effect, effect.ToString());
    }

    /// <summary>Gets the closed classification of the retained safe value.</summary>
    public SecurityAuditValueKind Kind { get; }

    /// <summary>Gets the already-redacted fingerprint or declared structural fact.</summary>
    /// <value>This value never contains raw prompts, arguments, results, credentials, or unclassified content.</value>
    public string Value { get; }
}
