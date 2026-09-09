// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one closed portable content item in an authoritative terminal tool result.</summary>
/// <remarks>Derived variants retain typed content without activating CLR types or inferring terminal status.</remarks>
public abstract record ToolResultContent
{
    /// <summary>Restricts the closed terminal-content family to this assembly.</summary>
    /// <remarks>Concrete variants expose their own validated construction contracts.</remarks>
    private protected ToolResultContent()
    {
    }

    /// <summary>Allows generated record copies only when the source has the same concrete runtime type.</summary>
    /// <param name="original">The nonnull same-variant value being copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="original"/> has a different runtime type from the value under construction.</exception>
    /// <remarks>C# record inheritance requires protected copy construction. This guard prevents an external derived record from creating its first valid instance by copying a built-in variant.</remarks>
    protected ToolResultContent(ToolResultContent original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
