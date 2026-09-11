// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Owns one isolated catalog composition and its test resources.</summary>
/// <remarks>Fixtures install exactly the supplied snapshots through the implementation's public composition surface.</remarks>
public abstract class ToolResultProjectionPolicyCatalogFixture: IAsyncDisposable
{
    /// <summary>Gets the composed catalog under test.</summary>
    /// <value>The nonnull catalog for this fixture's lifetime.</value>
    public abstract IToolResultProjectionPolicyCatalog Catalog { get; }

    /// <summary>Releases all resources owned by the fixture.</summary>
    /// <returns>Completion of cleanup without disposing resources owned by another fixture.</returns>
    public abstract ValueTask DisposeAsync();
}
