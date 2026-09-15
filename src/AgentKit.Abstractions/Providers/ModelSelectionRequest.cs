// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Everything selection needs in order to choose one configured model, with
/// no authority to perform provider I/O.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// The catalog snapshot is passed in rather than resolved by the selector, so
/// that one turn's selection, capability validation, and diagnostics all
/// describe the same catalog version even if the catalog reloads mid-run.
/// </para>
/// <para>
/// Selection is a pure decision. This request deliberately carries no
/// credential, endpoint, or grant, because choosing a model must not be able
/// to authorize reaching one.
/// </para>
/// </remarks>
public sealed record ModelSelectionRequest
{
    private readonly ModelSelectionPolicy _policy;
    private readonly ModelRequirements _requirements;
    private readonly ModelCatalogSnapshot _catalog;
    private readonly SecurityAuthorizationScope _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSelectionRequest"/>
    /// record.
    /// </summary>
    /// <param name="scope">
    /// The agent, session, and causal correlation this selection belongs to,
    /// used for diagnostics and traceability.
    /// </param>
    /// <param name="modelRequestId">
    /// The model request this selection is for, allocated before selection so
    /// the same identity flows through the attempt and its telemetry.
    /// </param>
    /// <param name="policy">The configured candidate and fallback policy.</param>
    /// <param name="requirements">The behaviors the request needs.</param>
    /// <param name="catalog">The catalog snapshot to choose from.</param>
    /// <param name="turnId">
    /// The turn this selection belongs to, when it belongs to one.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scope"/>, <paramref name="policy"/>,
    /// <paramref name="requirements"/>, or <paramref name="catalog"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="modelRequestId"/> is its default, empty identity, or
    /// <paramref name="turnId"/> is supplied as a default, empty identity.
    /// </exception>
    public ModelSelectionRequest(
        SecurityAuthorizationScope scope,
        ModelRequestId modelRequestId,
        ModelSelectionPolicy policy,
        ModelRequirements requirements,
        ModelCatalogSnapshot catalog,
        TurnId? turnId = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default, nameof(modelRequestId));
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(catalog);
        ThrowIfDefaultTurn(turnId, nameof(turnId));

        _scope = scope;
        ModelRequestId = modelRequestId;
        _policy = policy;
        _requirements = requirements;
        _catalog = catalog;
        TurnId = turnId;
    }

    /// <summary>Gets the agent, session, and causal correlation.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public SecurityAuthorizationScope Scope
    {
        get => _scope;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Scope));
            _scope = value;
        }
    }

    /// <summary>Gets the model request this selection is for.</summary>
    /// <value>
    /// A non-default identity that a loop must reuse for the first attempt the selection governs, so selection
    /// diagnostics correlate to a real attempt rather than a throwaway identity. A loop that selects once per run
    /// (as the first-party loop does) reuses it for the first turn's attempt and allocates fresh identities for
    /// later turns.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public ModelRequestId ModelRequestId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(ModelRequestId));
            field = value;
        }
    }

    /// <summary>Gets the configured candidate and fallback policy.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelSelectionPolicy Policy
    {
        get => _policy;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Policy));
            _policy = value;
        }
    }

    /// <summary>Gets the behaviors the request needs.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelRequirements Requirements
    {
        get => _requirements;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Requirements));
            _requirements = value;
        }
    }

    /// <summary>Gets the catalog snapshot to choose from.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelCatalogSnapshot Catalog
    {
        get => _catalog;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Catalog));
            _catalog = value;
        }
    }

    /// <summary>
    /// Gets the turn this selection belongs to, or <see langword="null"/> for
    /// model work that occurs outside a turn.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer supplies a default, empty identity. Absence is
    /// expressed as <see langword="null"/>.
    /// </exception>
    public TurnId? TurnId
    {
        get;
        init
        {
            ThrowIfDefaultTurn(value, nameof(TurnId));
            field = value;
        }
    }

    private static void ThrowIfDefaultTurn(TurnId? turnId, string paramName)
    {
        if (turnId is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, paramName);
        }
    }
}
