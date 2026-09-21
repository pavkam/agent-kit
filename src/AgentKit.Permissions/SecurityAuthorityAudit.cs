// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Builds redacted audit evidence for <see cref="SecurityAuthority"/> request and decision transitions.</summary>
internal static class SecurityAuthorityAudit
{
    internal const string DecisionCodeField = "decisionCode";
    internal const string WinningPolicyCodeField = "winningPolicyCode";
    internal const string ConstraintSummaryField = "constraintSummary";

    /// <summary>Creates the immutable field map for one normalized request entering evaluation.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>Named redacted values safe for restricted audit delivery.</returns>
    internal static ImmutableDictionary<string, RedactedAuditValue> CreateRequestFields(SecurityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImmutableDictionary.CreateRange(StringComparer.Ordinal, [
            new KeyValuePair<string, RedactedAuditValue>(
                "operationKind",
                RedactedAuditValue.FromOperationKind(request.Kind)),
            new KeyValuePair<string, RedactedAuditValue>(
                "effect",
                RedactedAuditValue.FromEffect(request.Effect)),
            new KeyValuePair<string, RedactedAuditValue>(
                "audience",
                RedactedAuditValue.FromComponentId(request.Audience)),
            new KeyValuePair<string, RedactedAuditValue>(
                "inputFingerprint",
                RedactedAuditValue.FromFingerprint(new ContentHash(request.InputFingerprint.Value))),
        ]);
    }

    /// <summary>Creates the immutable field map for one terminal policy decision.</summary>
    /// <param name="decisionCode">The stable decision or denial code.</param>
    /// <param name="winningPolicyCode">The contributing allow or deny policy code when known.</param>
    /// <param name="intersection">The intersected allow bounds when evaluation reached intersection.</param>
    /// <returns>Named redacted values safe for restricted audit delivery.</returns>
    internal static ImmutableDictionary<string, RedactedAuditValue> CreateDecisionFields(
        string decisionCode,
        string? winningPolicyCode,
        SecurityAllowConstraints? intersection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionCode);
        var fields = new Dictionary<string, RedactedAuditValue>(StringComparer.Ordinal)
        {
            [DecisionCodeField] = RedactedAuditValue.FromFingerprint(new ContentHash(decisionCode)),
        };
        if (winningPolicyCode is { Length: > 0 } policyCode)
        {
            fields[WinningPolicyCodeField] = RedactedAuditValue.FromFingerprint(new ContentHash(policyCode));
        }

        if (intersection is not null)
        {
            fields[ConstraintSummaryField] = RedactedAuditValue.FromFingerprint(
                new ContentHash(CreateConstraintSummary(intersection)));
        }

        return fields.ToImmutableDictionary();
    }

    private static string CreateConstraintSummary(SecurityAllowConstraints intersection)
    {
        var summary =
            $"resources:{intersection.Resources?.Length ?? 0}|effect:{intersection.Effect?.ToString() ?? "none"}|notBefore:{intersection.NotBefore?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? "none"}|expiresAt:{intersection.ExpiresAt?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? "none"}|allowedUses:{intersection.AllowedUses?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(summary))).ToLowerInvariant();
        return $"sha256:{digest}";
    }
}

/// <summary>Collects policy-evaluation evidence for decision audit records.</summary>
internal sealed class AuthorizationEvaluationTrace
{
    /// <summary>Records the latest explicit allow or approval-conditioned policy code.</summary>
    /// <param name="policyCode">The nonblank policy result code.</param>
    internal void RecordContributingPolicy(string policyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyCode);
        WinningAllowPolicyCode = policyCode;
    }

    /// <summary>Gets the intersected allow bounds when intersection succeeded.</summary>
    internal SecurityAllowConstraints? Intersection { get; private set; }

    /// <summary>Retains the intersected allow bounds for audit evidence.</summary>
    /// <param name="intersection">The merged bounds.</param>
    internal void SetIntersection(SecurityAllowConstraints intersection) => Intersection = intersection;

    /// <summary>Gets the last contributing allow policy code, if any.</summary>
    internal string? WinningAllowPolicyCode { get; private set; }
}
