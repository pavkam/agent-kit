// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Supplies an isolated <see cref="IMemoryStore"/> composition to <see cref="MemoryStoreConformanceTests{TFixture}"/>.</summary>
/// <remarks>Each fixture instance owns fresh storage, so cases never observe one another's state.</remarks>
public interface IMemoryStoreConformanceFixture
{
    /// <summary>Gets the capabilities the adapter claims; durability cases run only when it claims durability.</summary>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Gets the grant harness shared with the store under test.</summary>
    public TestGoalGrants Grants { get; }

    /// <summary>Gets the store under test.</summary>
    public IMemoryStore Store { get; }

    /// <summary>Opens a fresh store instance over the same storage, as a restarted host would.</summary>
    /// <param name="cancellationToken">Cancels reopening.</param>
    /// <returns>A new store over the persisted state, or the same store when the adapter is ephemeral.</returns>
    public ValueTask<IMemoryStore> ReopenAsync(CancellationToken cancellationToken = default);
}
