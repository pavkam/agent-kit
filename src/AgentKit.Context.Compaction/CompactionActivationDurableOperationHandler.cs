// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Owns the compaction-activation boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating this package's registration adds
/// no second handler for the same operation name, which the coordinator rejects outright. The activation append is
/// performed by the live activation coordinator, which publishes its continuation into the shared
/// <see cref="DurableBoundaryRegistry"/>; a recovering process holds none and refuses rather than appending a second
/// compaction record whose candidate it never computed.
/// </remarks>
public sealed class CompactionActivationDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the compaction-activation handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the running activation publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public CompactionActivationDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(CompactionDurableOperations.Activation, registry)
    {
    }
}
