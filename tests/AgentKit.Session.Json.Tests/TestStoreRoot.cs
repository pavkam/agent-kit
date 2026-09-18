// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Owns one isolated temporary store root and composes a raw, uninitialized <see cref="JsonSessionStore"/> over it.</summary>
/// <remarks>
/// Unlike <see cref="JsonSessionStoreConformanceFixture"/>, this helper never calls <see cref="JsonSessionStore.InitializeAsync"/>
/// and never disposes a previously opened store for the caller, so tests can exercise uninitialized use, manifest
/// mismatches, torn-append recovery, exclusive-lock contention, and multiple reopens with different settings over the
/// exact same root.
/// </remarks>
internal sealed class TestStoreRoot: IDisposable
{
    private readonly string _path = TestTemporaryDirectory.Create();
    private readonly List<ServiceProvider> _providers = [];

    /// <summary>Gets the fully qualified store root directory this instance owns.</summary>
    internal string DirectoryPath => Path.Combine(_path, "sessions");

    /// <summary>Gets the fixed persistent store identity every open of this root must present.</summary>
    internal JsonSessionStoreInstanceId InstanceId { get; } = new(Guid.NewGuid());

    /// <summary>Composes one uninitialized store bound to this root without opening, creating, or locking it.</summary>
    /// <param name="auditDispatcher">The audit dispatcher backing the composed store.</param>
    /// <param name="grantStore">The grant store backing the composed store.</param>
    /// <param name="timeProvider">The clock backing the composed store.</param>
    /// <param name="settings">The settings to bind; defaults to <see cref="JsonSessionStoreSettings.CreateDefault"/>.</param>
    /// <param name="openMode">Whether the root may be created.</param>
    /// <param name="recoveryMode">Whether a torn trailing append may be discarded.</param>
    /// <param name="instanceId">Overrides the expected persistent identity; defaults to <see cref="InstanceId"/>.</param>
    /// <returns>
    /// A new store the caller owns. Disposing the store releases the root's exclusive lock; the backing composition
    /// provider is disposed together with every other provider this instance created when <see cref="Dispose"/> runs, so
    /// two stores opened over the same root may coexist to exercise lock contention.
    /// </returns>
    internal JsonSessionStore Open(
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        JsonSessionStoreSettings? settings = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends,
        JsonSessionStoreInstanceId? instanceId = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(timeProvider);
        _ = services.AddSingleton(auditDispatcher);
        _ = services.AddSingleton(grantStore);
        _ = services.AddJsonSessionStore(
            new JsonSessionStoreTarget(DirectoryPath, instanceId ?? InstanceId, openMode, recoveryMode),
            settings ?? JsonSessionStoreSettings.CreateDefault());
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return (JsonSessionStore) provider.GetRequiredService<ISessionStore>();
    }

    /// <summary>Removes the temporary root and everything a case wrote into it.</summary>
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }
    }
}
