// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;
using System.Diagnostics;

/// <summary>Builds and validates the atomic receipt required before a human question is published.</summary>
/// <remarks>The receipt is grant-store evidence for one exact publication. It neither proves the application channel displayed the prompt nor that a selected store retains evidence durably.</remarks>
internal static class HumanQuestionEnforcementReceipt
{
    /// <summary>Determines whether captured authorization remains exact for the requested question publication.</summary>
    /// <param name="request">The non-null question publication request.</param>
    /// <returns><see langword="true"/> when no authorization was captured or its scope and identity exactly match the requested effect.</returns>
    /// <remarks>A mismatch is a runtime authorization denial, not an argument-validation failure, because independently valid request and grant evidence can become unrelated before publication.</remarks>
    internal static bool HasCompatibleCapturedAuthorization(HumanQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        return request.Grant.Authorization is not { } authorization
            || (authorization.Scope == scope && authorization.Identity == request.Identity);
    }

    /// <summary>Creates exact human-question publication evidence while retaining captured authorization from the grant.</summary>
    /// <param name="request">The non-null question publication request.</param>
    /// <param name="audience">The concrete broker consuming the grant.</param>
    /// <returns>Concrete enforcement evidence that retains compatible captured authorization selection.</returns>
    internal static SecurityEnforcementRequest Create(HumanQuestionRequest request, ComponentId audience)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resources = ImmutableArray.Create(HumanQuestionSecurityBinding.Resource(request.Id));
        var fingerprint = HumanQuestionSecurityBinding.Fingerprint(
            request.Id,
            request.Prompt,
            request.Options,
            request.AllowsFreeText,
            request.Deadline);
        return request.Grant.Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation),
                request.Identity,
                authorization,
                audience,
                SecurityOperationKind.StateMutation,
                SecurityEffect.Create,
                resources,
                fingerprint,
                request.Grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation),
                request.Identity,
                audience,
                SecurityOperationKind.StateMutation,
                SecurityEffect.Create,
                resources,
                fingerprint,
                request.Grant.RevocationVersion);
    }

    /// <summary>Determines whether a result permits this fresh exact question publication.</summary>
    /// <param name="consumption">The non-null atomic grant-store result.</param>
    /// <param name="grant">The non-null grant presented by the broker.</param>
    /// <param name="enforcement">The non-null evidence created by this broker.</param>
    /// <param name="intent">The non-null fresh identity created for this publication attempt.</param>
    /// <returns><see langword="true"/> only when the grant store retained a complete receipt for this new consumption.</returns>
    internal static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        return consumption.Status is GrantConsumptionStatus.Consumed
            && consumption.IntentReceipt is { } receipt
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && HasExactEnforcement(receipt.Enforcement, enforcement);
    }

    /// <summary>Returns a stable non-content failure message for a result that does not authorize fresh publication.</summary>
    /// <param name="consumption">The non-null result that failed fresh receipt validation.</param>
    /// <returns>The store message for an unconsumed result, or the fixed missing-receipt message for invalid consumed evidence.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }

    /// <summary>Compares all value fields and ordered resource contents without relying on immutable-array backing identity.</summary>
    /// <param name="actual">The enforcement retained by the validated receipt.</param>
    /// <param name="expected">The enforcement created by this broker.</param>
    /// <returns><see langword="true"/> when the retained evidence is exactly the question publication being performed.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this broker.");
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
}
