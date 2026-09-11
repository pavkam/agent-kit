// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Marks an explicitly published toolset key for composition-time capture.</summary>
/// <remarks>The marker carries no container or mutable publication state.</remarks>
internal sealed record ToolsetRegistration
{
    /// <summary>Captures a toolset key without resolving or activating a registration.</summary>
    /// <param name="key">The nondefault exact typed toolset key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    internal ToolsetRegistration(ToolsetKey key)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        Key = key;
    }
    /// <summary>Gets the toolset key whose exact publication is captured at composition.</summary>
    /// <value>A nondefault ordinal key; no version or policy family is inferred.</value>
    internal ToolsetKey Key { get; }
}
