// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces the protected resources and canonical fingerprints that bind a security grant to one exact goal-store operation.</summary>
/// <remarks>
/// A goal store derives the same resource and fingerprint from the request it is executing and asks the grant store to
/// consume a grant that binds them, so a grant issued for one goal, version, or payload cannot authorize another. Reads use
/// <see cref="SecurityOperationKind.StateRead"/> and writes use <see cref="SecurityOperationKind.StateMutation"/>.
/// </remarks>
public static class GoalSecurityBinding
{
    /// <summary>Names one goal as a protected resource.</summary>
    /// <param name="goalId">The goal identity.</param>
    /// <returns>The protected goal resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="goalId"/> is default.</exception>
    public static ProtectedResource Resource(GoalId goalId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        return new(ProtectedResourceKind.ApplicationState, $"goal:{goalId}");
    }

    /// <summary>Names the set of one goal's children as a protected resource.</summary>
    /// <param name="parentId">The parent goal identity.</param>
    /// <returns>The protected children resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentId"/> is default.</exception>
    public static ProtectedResource ChildrenResource(GoalId parentId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(parentId, default);
        return new(ProtectedResourceKind.ApplicationState, $"goal-children:{parentId}");
    }

    /// <summary>Fingerprints a goal creation, including any delegation that creates it as a child.</summary>
    /// <param name="request">The create request; its grant is excluded.</param>
    /// <returns>A deterministic fingerprint of the goal, delegation, and idempotency key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(GoalCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateFingerprint(request.Goal, request.Delegation, request.IdempotencyKey);
    }

    /// <summary>Fingerprints a goal creation from its parts, so a coordinator can bind a grant before it builds the store request.</summary>
    /// <param name="goal">The goal to create.</param>
    /// <param name="delegation">The delegation that creates it as a child, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The creation replay key.</param>
    /// <returns>The same fingerprint <see cref="Fingerprint(GoalCreateRequest)"/> yields for a request with these parts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="goal"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public static InputFingerprint CreateFingerprint(AgentGoal goal, DelegationRequest? delegation, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new CreatePayload(goal, delegation, idempotencyKey));
    }

    /// <summary>Fingerprints a goal load.</summary>
    /// <param name="request">The load request; its grant is excluded.</param>
    /// <returns>A deterministic fingerprint of the profile and goal identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(GoalLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return LoadFingerprint(request.Profile, request.GoalId);
    }

    /// <summary>Fingerprints a goal load from its parts.</summary>
    /// <param name="profile">The captured profile.</param>
    /// <param name="goalId">The goal to load.</param>
    /// <returns>The same fingerprint <see cref="Fingerprint(GoalLoadRequest)"/> yields for a request with these parts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="goalId"/> is default.</exception>
    public static InputFingerprint LoadFingerprint(GoalProfileReference profile, GoalId goalId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        return SecurityCanonicalFingerprint.Create(new LoadPayload(profile, goalId));
    }

    /// <summary>Fingerprints a goal transition and its attempt mutation.</summary>
    /// <param name="request">The transition request; its grant is excluded.</param>
    /// <returns>A deterministic fingerprint of the profile, transition, and attempt change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(GoalTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TransitionFingerprint(request.Profile, request.Transition, request.Attempt);
    }

    /// <summary>Fingerprints a goal transition from its parts.</summary>
    /// <param name="profile">The captured profile.</param>
    /// <param name="transition">The transition.</param>
    /// <param name="attempt">The atomic attempt mutation, or <see langword="null"/>.</param>
    /// <returns>The same fingerprint <see cref="Fingerprint(GoalTransitionRequest)"/> yields for a request with these parts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> or <paramref name="transition"/> is null.</exception>
    public static InputFingerprint TransitionFingerprint(GoalProfileReference profile, GoalTransition transition, GoalAttemptChange? attempt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(transition);
        return SecurityCanonicalFingerprint.Create(new TransitionPayload(profile, transition, attempt));
    }

    /// <summary>Fingerprints a children page read.</summary>
    /// <param name="request">The children request; its grant is excluded.</param>
    /// <returns>A deterministic fingerprint of the profile, parent, cursor, and page size.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(GoalChildrenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChildrenFingerprint(request.Profile, request.ParentId, request.AfterOrdinal, request.Limit);
    }

    /// <summary>Fingerprints a children page read from its parts.</summary>
    /// <param name="profile">The captured profile.</param>
    /// <param name="parentId">The parent goal.</param>
    /// <param name="afterOrdinal">The exclusive cursor.</param>
    /// <param name="limit">The page size.</param>
    /// <returns>The same fingerprint <see cref="Fingerprint(GoalChildrenRequest)"/> yields for a request with these parts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentId"/> is default, <paramref name="afterOrdinal"/> is negative, or <paramref name="limit"/> is not positive.</exception>
    public static InputFingerprint ChildrenFingerprint(GoalProfileReference profile, GoalId parentId, long afterOrdinal, int limit)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(parentId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(afterOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return SecurityCanonicalFingerprint.Create(new ChildrenPayload(profile, parentId, afterOrdinal, limit));
    }

    private sealed record CreatePayload(AgentGoal Goal, DelegationRequest? Delegation, IdempotencyKey IdempotencyKey);

    private sealed record LoadPayload(GoalProfileReference Profile, GoalId GoalId);

    private sealed record TransitionPayload(GoalProfileReference Profile, GoalTransition Transition, GoalAttemptChange? Attempt);

    private sealed record ChildrenPayload(GoalProfileReference Profile, GoalId ParentId, long AfterOrdinal, int Limit);
}
