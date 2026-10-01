// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provides the canonical default <see cref="ComponentKey{TContract}"/> values for the keyed
/// <see cref="IModelSelector"/> and <see cref="IModelRequestExecutor"/> an agent definition selects through
/// <see cref="AgentComponentSelection.ModelSelector"/> and <see cref="AgentComponentSelection.ModelExecutor"/>.
/// </summary>
/// <remarks>
/// The first-party provider runtime registers its default selector and executor under these keys, so an
/// otherwise-unconfigured composition can select them by value. A host that registers an alternative selector or
/// executor under its own key selects that key per definition instead. This type is declarative key metadata only; it
/// carries no service instance and performs no registration.
/// </remarks>
public static class AgentProviderComponentDefaults
{
    /// <summary>The string value of <see cref="ModelSelectorKey"/>, exposed as a compile-time constant.</summary>
    public const string ModelSelectorKeyValue = "agentkit-default-model-selector";

    /// <summary>Gets the canonical default key under which the first-party <see cref="IModelSelector"/> is registered.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or selects the default selector.</value>
    public static ComponentKey<IModelSelector> ModelSelectorKey { get; } = new(ModelSelectorKeyValue);

    /// <summary>The string value of <see cref="ModelExecutorKey"/>, exposed as a compile-time constant.</summary>
    public const string ModelExecutorKeyValue = "agentkit-default-model-executor";

    /// <summary>Gets the canonical default key under which the first-party <see cref="IModelRequestExecutor"/> is registered.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or selects the default executor.</value>
    public static ComponentKey<IModelRequestExecutor> ModelExecutorKey { get; } = new(ModelExecutorKeyValue);
}
