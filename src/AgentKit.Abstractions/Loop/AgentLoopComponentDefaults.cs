// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provides the canonical, engine-wide default <see cref="ComponentKey{TContract}"/> for
/// <see cref="IAgentLoop"/> selection, shared by the facade and every first-party loop package without either
/// depending on the other's concrete assembly.
/// </summary>
/// <remarks>
/// <para>
/// The facade (<c>AgentKit</c>) resolves the keyed <see cref="IAgentLoop"/> selected by an
/// <see cref="AgentDefinition"/> for each run, substituting this default key when a definition leaves
/// <see cref="AgentDefinition.LoopKey"/> unset. <c>AgentKit</c> must never reference a concrete loop
/// implementation package such as <c>AgentKit.Loop</c> merely to learn this key's text, so the canonical value
/// lives here in the shared abstractions package instead. A first-party loop registration helper (for example
/// <c>AgentKit.Loop</c>'s <c>AddAgentLoop</c>) uses the exact same value so that a host calling
/// <c>services.AddAgentLoop(AgentLoopComponentDefaults.LoopKey)</c> registers precisely the key every
/// unconfigured definition already resolves to.
/// </para>
/// <para>
/// This type carries no service instance and performs no registration itself; it is declarative key metadata
/// only.
/// </para>
/// </remarks>
public static class AgentLoopComponentDefaults
{
    /// <summary>
    /// The string value of <see cref="LoopKey"/>, exposed as a compile-time constant so callers that need the
    /// raw text (for example, a keyed-registration helper or a diagnostic message) do not need to allocate a
    /// <see cref="ComponentKey{TContract}"/> merely to read <see cref="ComponentKey{TContract}.Value"/>.
    /// </summary>
    public const string LoopKeyValue = "agentkit-default-loop";

    /// <summary>Gets the canonical default key selecting an agent's <see cref="IAgentLoop"/> when its definition names none explicitly.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or resolves a keyed loop.</value>
    public static ComponentKey<IAgentLoop> LoopKey { get; } = new(LoopKeyValue);

    /// <summary>
    /// The string value of <see cref="ContinuationPolicyKey"/>, exposed as a compile-time constant for the same
    /// reason as <see cref="LoopKeyValue"/>.
    /// </summary>
    /// <remarks>
    /// This value is the single source of truth for the canonical continuation-policy key. The first-party loop
    /// package's own <c>AgentLoopDefaults.ContinuationPolicyKey</c> delegates to this value rather than
    /// declaring an independent constant, so the facade's run-activation boundary — which cannot reference the
    /// concrete loop package — and the loop package's own registration helpers always agree on the same text.
    /// </remarks>
    public const string ContinuationPolicyKeyValue = "agentkit-default-continuation";

    /// <summary>Gets the key of the canonical stateless continuation policy every reduced loop consults by default.</summary>
    public static ComponentKey<IRunContinuationPolicy> ContinuationPolicyKey { get; } = new(ContinuationPolicyKeyValue);
}
