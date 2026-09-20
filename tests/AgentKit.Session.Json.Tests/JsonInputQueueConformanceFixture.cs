// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Conformance;

/// <summary>Composes the session-backed input queue over the real JSON session store.</summary>
public sealed class JsonInputQueueConformanceFixture: SessionBackedInputQueueConformanceFixture
{
    private readonly string _directory = TestTemporaryDirectory.Create();

    /// <inheritdoc/>
    protected override string StoreKey => "agentkit.json";

    /// <inheritdoc/>
    protected override void RegisterStore(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var storeRoot = Path.Combine(_directory, "sessions");
        var directoryRoot = Path.Combine(_directory, "directory");
        _ = Directory.CreateDirectory(storeRoot);
        _ = Directory.CreateDirectory(directoryRoot);
        _ = services.AddJsonSessionStore(new JsonSessionStoreTarget(
            storeRoot, new JsonSessionStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends));
        _ = services.AddJsonSessionDirectory(
            new ComponentId("conformance-directory"),
            new JsonSessionDirectoryTarget(
                directoryRoot, new JsonSessionDirectoryInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends));
    }

    /// <inheritdoc/>
    protected override async ValueTask PrepareStoreAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await services.GetRequiredService<ISessionStore>().ShouldBeOfType<JsonSessionStore>()
            .InitializeAsync(cancellationToken);
        await services.GetRequiredService<ISessionDirectory>().ShouldBeOfType<JsonSessionDirectory>()
            .InitializeAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask ReleaseStoreAsync()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Disposal already dropped the advisory lock; a racing reader must not fail the fixture teardown.
        }

        return ValueTask.CompletedTask;
    }
}
