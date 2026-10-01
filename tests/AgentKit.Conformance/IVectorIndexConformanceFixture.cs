// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Supplies isolated <see cref="IVectorIndex"/> compositions to <see cref="VectorIndexConformanceTests{TFixture}"/>.</summary>
/// <remarks>Each fixture instance owns fresh storage, so cases never observe one another's state.</remarks>
public interface IVectorIndexConformanceFixture
{
    /// <summary>Gets the capabilities the adapter claims; durability cases run only when it claims durability.</summary>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Gets the grant harness shared with the indexes under test.</summary>
    public TestGoalGrants Grants { get; }

    /// <summary>Gets the default index, whose space is <see cref="MemoryTestData.Space(string, int, VectorDistanceMetric, string)"/> with its defaults.</summary>
    public IVectorIndex Index { get; }

    /// <summary>Creates a second, independent index over fresh storage for a specific space.</summary>
    /// <param name="space">The space the new index holds.</param>
    /// <returns>A new empty index.</returns>
    public IVectorIndex CreateIndex(VectorSpaceDescriptor space);

    /// <summary>Opens a fresh instance of the default index over the same storage, as a restarted host would.</summary>
    /// <param name="cancellationToken">Cancels reopening.</param>
    /// <returns>A new index over the persisted state, or the same index when the adapter is ephemeral.</returns>
    public ValueTask<IVectorIndex> ReopenAsync(CancellationToken cancellationToken = default);
}
