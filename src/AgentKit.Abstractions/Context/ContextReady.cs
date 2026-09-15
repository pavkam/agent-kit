// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Context assembly produced a complete, provider-ready request.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Every repair
/// the assembler applied while turning durable history into the request view
/// is listed in <see cref="Repairs"/> so the exclusion of a message is
/// attributable to its source identity rather than silent.
/// </remarks>
public sealed record ContextReady: ContextAssemblyResult
{
    /// <summary>Initializes a result that applied no history repairs.</summary>
    /// <param name="context">The assembled, provider-ready request content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public ContextReady(LlmRequestContext context)
        : this(context, [])
    {
    }

    /// <summary>Initializes a result together with the ordered repairs that produced it.</summary>
    /// <param name="context">The assembled, provider-ready request content.</param>
    /// <param name="repairs">The initialized, ordered repair evidence; empty when nothing was repaired.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="repairs"/> is a default, uninitialized array or contains a null element.</exception>
    public ContextReady(LlmRequestContext context, ImmutableArray<HistoryRepair> repairs)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfContainsNull(repairs);
        Context = context;
        Repairs = repairs;
    }

    /// <summary>Gets the assembled, provider-ready request content.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public LlmRequestContext Context
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the ordered repairs applied to the durable history while building <see cref="Context"/>.</summary>
    /// <value>An initialized sequence without null elements, in source-history order; empty when no message was excluded or altered.</value>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is a default,
    /// uninitialized array or contains a null element.
    /// </exception>
    public ImmutableArray<HistoryRepair> Repairs
    {
        get;
        init
        {
            ArgumentException.ThrowIfContainsNull(value);
            field = value;
        }
    }

    /// <summary>Compares the request content and the ordered repair evidence.</summary>
    /// <param name="other">The result to compare.</param>
    /// <returns><see langword="true"/> when the context and every ordered repair are equal.</returns>
    public bool Equals(ContextReady? other) =>
        other is not null && Context.Equals(other.Context) && Repairs.SequenceEqual(other.Repairs);

    /// <summary>Returns a hash compatible with ordered structural equality.</summary>
    /// <returns>A hash over the context and every repair.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Context);
        foreach (var repair in Repairs)
        {
            hash.Add(repair);
        }

        return hash.ToHashCode();
    }
}
