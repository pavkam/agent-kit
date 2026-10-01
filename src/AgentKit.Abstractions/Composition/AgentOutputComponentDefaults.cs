// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides the canonical default <see cref="ComponentKey{TContract}"/> for the first-party <see cref="IOutputProcessor"/>.</summary>
/// <remarks>
/// A definition selects its processor through <see cref="AgentComponentSelection.OutputProcessor"/>. AgentKit.Output
/// registers its default processor profile under this key when its registration overloads omit one, so the facade,
/// composition helpers, and hosts agree on the key text without referencing the concrete package. This type is
/// declarative key metadata only; it carries no service instance and performs no registration.
/// </remarks>
public static class AgentOutputComponentDefaults
{
    /// <summary>The string value of <see cref="ProcessorKey"/>, exposed as a compile-time constant.</summary>
    public const string ProcessorKeyValue = "agentkit-default-output";

    /// <summary>Gets the key used by registration overloads that omit an output-processor profile.</summary>
    /// <value>The stable <c>agentkit-default-output</c> processor key.</value>
    public static ComponentKey<IOutputProcessor> ProcessorKey { get; } = new(ProcessorKeyValue);
}
