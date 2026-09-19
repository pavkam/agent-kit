// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The requested ordering preference carried on one hook registration descriptor.</summary>
/// <remarks>
/// <see cref="Anchor"/> is the only ordering fact this value carries today; it is a dedicated type rather than a
/// bare <see cref="HookOrderAnchor"/> field so a future ordering refinement (for example, a numeric tie-break
/// within <see cref="HookOrderAnchor.Normal"/>) can be added without a breaking change to the registration
/// descriptor. This type is an immutable value object with structural equality, safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record HookOrder
{
    /// <summary>Gets the shared instance requesting no coarse anchor.</summary>
    public static HookOrder Normal { get; } = new(HookOrderAnchor.Normal);

    /// <summary>Gets the shared instance requesting the <see cref="HookOrderAnchor.First"/> anchor.</summary>
    public static HookOrder First { get; } = new(HookOrderAnchor.First);

    /// <summary>Gets the shared instance requesting the <see cref="HookOrderAnchor.Last"/> anchor.</summary>
    public static HookOrder Last { get; } = new(HookOrderAnchor.Last);

    /// <summary>Initializes a new instance of the <see cref="HookOrder"/> record.</summary>
    /// <param name="anchor">The coarse ordering anchor this registration requests.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="anchor"/> is not a defined <see cref="HookOrderAnchor"/>.</exception>
    public HookOrder(HookOrderAnchor anchor)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(anchor);
        Anchor = anchor;
    }

    /// <summary>Gets the coarse ordering anchor this registration requests.</summary>
    public HookOrderAnchor Anchor { get; }
}
