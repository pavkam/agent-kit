// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

/// <summary>Composes an isolated JSON reference-commit intent store over a temporary root through its normal registration.</summary>
public sealed class JsonArtifactReferenceCommitIntentStoreConformanceFixture: IArtifactReferenceCommitIntentStoreConformanceFixture
{
    private ServiceProvider? _provider;
    private JsonArtifactReferenceCommitIntentStore? _reopened;

    /// <summary>Gets the root the store under test uses.</summary>
    internal JsonArtifactTestRoot Root { get; } = new();

    /// <summary>Gets the path of the intent log of the default store root.</summary>
    internal string LogPath => Path.Combine(Root.StoreDirectory("store"), "intents.jsonl");

    /// <summary>Closes the current store, releasing its lock, and opens a fresh one over the same root.</summary>
    /// <returns>The reopened store, which shares no in-process state with the closed one.</returns>
    internal JsonArtifactReferenceCommitIntentStore Reopen()
    {
        _provider?.Dispose();
        _provider = null;
        _reopened?.Dispose();
        _reopened = new JsonArtifactReferenceCommitIntentStore(Root.Target(), JsonArtifactSettings.CreateDefault(), TimeProvider.System);
        return _reopened;
    }

    /// <inheritdoc/>
    public ValueTask<IArtifactReferenceCommitIntentStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _provider ??= new ServiceCollection()
            .AddJsonArtifactReferenceCommitIntentStore(Root.Target())
            .BuildServiceProvider();
        return ValueTask.FromResult(_provider.GetRequiredService<IArtifactReferenceCommitIntentStore>());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        _reopened?.Dispose();
        Root.Dispose();
    }
}
