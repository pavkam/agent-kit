// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provides the canonical, engine-wide default <see cref="ComponentKey{TContract}"/> values for
/// <see cref="IInputCoordinator"/> and <see cref="IOutputPublisher"/> selection, shared by the facade and
/// AgentKit.IO without either depending on the other's concrete assembly.
/// </summary>
/// <remarks>
/// AgentKit.IO registers its default coordinator and publisher under these keys, so a definition selects them through
/// <see cref="AgentComponentSelection.Input"/> and <see cref="AgentComponentSelection.Output"/> by value, mirroring
/// <see cref="AgentLoopComponentDefaults.LoopKey"/>'s role for <see cref="IAgentLoop"/> selection. This type carries
/// no service instance and performs no registration itself; it is declarative key metadata only.
/// </remarks>
public static class AgentIOComponentDefaults
{
    /// <summary>
    /// The string value of <see cref="InputCoordinatorKey"/>, exposed as a compile-time constant for callers
    /// that need the raw text without allocating a <see cref="ComponentKey{TContract}"/>.
    /// </summary>
    public const string InputCoordinatorKeyValue = "agentkit-default-input-coordinator";

    /// <summary>Gets the canonical default key selecting an agent's <see cref="IInputCoordinator"/> when a definition selects the first-party default.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or resolves a keyed input coordinator.</value>
    public static ComponentKey<IInputCoordinator> InputCoordinatorKey { get; } = new(InputCoordinatorKeyValue);

    /// <summary>
    /// The string value of <see cref="OutputPublisherKey"/>, exposed as a compile-time constant for the same
    /// reason as <see cref="InputCoordinatorKeyValue"/>.
    /// </summary>
    public const string OutputPublisherKeyValue = "agentkit-default-output-publisher";

    /// <summary>Gets the canonical default key selecting an agent's <see cref="IOutputPublisher"/> when a definition selects the first-party default.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or resolves a keyed output publisher.</value>
    public static ComponentKey<IOutputPublisher> OutputPublisherKey { get; } = new(OutputPublisherKeyValue);
}
