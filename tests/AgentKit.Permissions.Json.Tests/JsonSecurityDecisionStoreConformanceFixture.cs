// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Creates one isolated initialized JSON decision-store root for each reusable contract case.</summary>
public sealed class JsonSecurityDecisionStoreConformanceFixture: ISecurityDecisionStoreConformanceFixture
{
    private readonly string _directory = TestTemporaryDirectory.Create();
    private readonly JsonSecurityDecisionStoreTarget _target;
    private ServiceProvider? _provider;
    private JsonSecurityDecisionStore? _store;

    /// <summary>Initializes a fixture with one fixed store root for the case lifetime.</summary>
    public JsonSecurityDecisionStoreConformanceFixture()
    {
        _target = new JsonSecurityDecisionStoreTarget(
            Path.Combine(_directory, "decisions"),
            new JsonSecurityDecisionStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends);
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new();

    /// <inheritdoc/>
    public async ValueTask<ISecurityDecisionStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddJsonSecurityDecisionStore(_target, JsonSecurityDecisionStoreSettings.CreateDefault());
            _provider = services.BuildServiceProvider(validateScopes: true);
            _store = _provider.GetRequiredService<ISecurityDecisionStore>().ShouldBeOfType<JsonSecurityDecisionStore>();
            await _store.InitializeAsync(SecurityControlPlaneTestBootstrap.Create(), cancellationToken);
        }

        return _store;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityDecision> ReadRecorded(ISecurityDecisionStore store) =>
        ((JsonSecurityDecisionStore) store).Decisions;

    /// <inheritdoc/>
    public async ValueTask<ISecurityDecisionStore> RecreateAsync(CancellationToken cancellationToken = default)
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
            _store = null;
        }

        return await CreateAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
