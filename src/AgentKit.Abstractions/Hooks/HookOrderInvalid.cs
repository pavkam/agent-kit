// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a point's registrations cannot be deterministically ordered.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. Causes include a missing hard <c>DependsOn</c> target, more than one
/// <see cref="HookOrderAnchor.First"/> or <see cref="HookOrderAnchor.Last"/> registration, a contradictory
/// <c>Before</c>/<c>After</c> pair, or an ordering cycle. <see cref="Diagnostics"/> carries one stable-coded
/// <see cref="CompositionDiagnostic"/> per detected problem so composition validation can surface every cause at
/// once rather than the first one found.
/// </remarks>
public sealed record HookOrderInvalid: HookOrderResult
{
    /// <summary>Initializes an invalid ordering result.</summary>
    /// <param name="diagnostics">The stable-coded diagnostics describing every detected ordering problem.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="diagnostics"/> is default, empty, or contains a null element.
    /// </exception>
    public HookOrderInvalid(ImmutableArray<CompositionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfContainsNull(diagnostics, nameof(diagnostics));
        ArgumentOutOfRangeException.ThrowIfZero(diagnostics.Length, nameof(diagnostics));
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the stable-coded diagnostics describing every detected ordering problem.</summary>
    public ImmutableArray<CompositionDiagnostic> Diagnostics { get; }

    /// <inheritdoc/>
    public bool Equals(HookOrderInvalid? other) => other is not null && Diagnostics.SequenceEqual(other.Diagnostics);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var diagnostic in Diagnostics)
        {
            hash.Add(diagnostic);
        }

        return hash.ToHashCode();
    }
}
