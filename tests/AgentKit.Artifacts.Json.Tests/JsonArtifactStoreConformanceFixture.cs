// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

/// <summary>Composes an isolated JSON artifact store over a temporary root and the shared deterministic grant authority.</summary>
public sealed class JsonArtifactStoreConformanceFixture: ArtifactStoreConformanceFixtureBase
{
    private JsonArtifactStore? _current;

    /// <summary>Gets the root the store under test uses.</summary>
    internal JsonArtifactTestRoot Root { get; } = new();

    /// <summary>Gets the authoritative grant store shared with every store this fixture opens.</summary>
    internal Permissions.InMemory.InMemorySecurityGrantStore GrantStore => Grants;

    /// <summary>Gets the deterministic clock shared with every store this fixture opens.</summary>
    internal TimeProvider ClockProvider => Clock;

    /// <summary>Closes the current store, releasing its lock, and opens a fresh one over the same root.</summary>
    /// <returns>The reopened store, which shares no in-process state with the closed one.</returns>
    internal JsonArtifactStore Reopen()
    {
        _current?.Dispose();
        _current = NewStore();
        return _current;
    }

    /// <inheritdoc/>
    protected override IArtifactStore CreateStore() => _current = NewStore();

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        _current?.Dispose();
        Root.Dispose();
        return ValueTask.CompletedTask;
    }

    private JsonArtifactStore NewStore() => new(Root.Target(), JsonArtifactSettings.CreateDefault(), Grants, new SequentialIntentIds(), Clock);
}
