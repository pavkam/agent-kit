// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>
/// The immutable, scope-compiled activation of one admitted run.
/// </summary>
/// <remarks>
/// <para>
/// The facade materializes this plan once per run so a key selected for one agent does not implicitly flow
/// into an unrelated constructor graph. Loops and collaborators receive the resolved values; none receives a
/// service provider.
/// </para>
/// <para>
/// <see cref="HookDispatchContext"/> is intentionally absent. Run-scoped hook activation has not landed, and
/// this plan does not invent a stand-in context. <see cref="OptionalCapabilities"/> is
/// <see cref="AgentOptionalCapabilitySelection.None"/> until a definition carries an explicit selection.
/// </para>
/// </remarks>
internal sealed record AgentRunPlan
{
    /// <summary>Captures one validated activation.</summary>
    /// <param name="definition">The pinned definition this run executes.</param>
    /// <param name="catalogVersion">The catalog version examined when the plan was compiled.</param>
    /// <param name="loop">The loop resolved under the definition's selected key.</param>
    /// <param name="services">The compiled collaborator bundle that loop drives.</param>
    /// <param name="session">The session capability installed into the run scope before collaborator resolution.</param>
    /// <param name="authorization">Fresh authorization captured for this admission, matching the pinned security publication.</param>
    /// <param name="optionalCapabilities">The optional capability selection, or <see cref="AgentOptionalCapabilitySelection.None"/>.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    internal AgentRunPlan(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        IAgentLoop loop,
        AgentRunServices services,
        SessionExecutionCapability session,
        SecurityAuthorizationContext authorization,
        AgentOptionalCapabilitySelection optionalCapabilities)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(optionalCapabilities);

        Definition = definition;
        CatalogVersion = catalogVersion;
        Loop = loop;
        Services = services;
        Session = session;
        Authorization = authorization;
        OptionalCapabilities = optionalCapabilities;
    }

    /// <summary>Gets the pinned definition this run executes.</summary>
    /// <value>The exact definition the compiler revalidated.</value>
    internal AgentDefinition Definition { get; }

    /// <summary>Gets the catalog version examined at compilation.</summary>
    /// <value>The snapshot version used to accept or reject the pinned definition.</value>
    internal AgentCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the loop selected for this definition.</summary>
    /// <value>The scoped instance resolved under the definition's loop key.</value>
    internal IAgentLoop Loop { get; }

    /// <summary>Gets the compiled collaborator bundle.</summary>
    /// <value>The immutable services the loop receives. It is not resolved again from the container.</value>
    internal AgentRunServices Services { get; }

    /// <summary>Gets the session capability installed for this scope.</summary>
    /// <value>The profile and coordinator instances later admission and release reuse.</value>
    internal SessionExecutionCapability Session { get; }

    /// <summary>Gets the authorization captured for this admission.</summary>
    /// <value>Evidence that matched the pinned security publication. It is not a grant to widen later effects.</value>
    internal SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the optional capabilities selected for this definition.</summary>
    /// <value>
    /// <see cref="AgentOptionalCapabilitySelection.None"/> until a definition publishes an explicit selection.
    /// </value>
    internal AgentOptionalCapabilitySelection OptionalCapabilities { get; }
}
