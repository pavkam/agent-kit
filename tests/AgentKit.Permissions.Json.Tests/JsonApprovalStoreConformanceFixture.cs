// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Creates one isolated initialized JSON store root for each reusable approval-store contract case.</summary>
public sealed class JsonApprovalStoreConformanceFixture: IApprovalStoreConformanceFixture
{
    private readonly string _directory = TestTemporaryDirectory.Create();
    private ServiceProvider? _provider;
    private JsonApprovalStore? _store;

    /// <inheritdoc/>
    public async ValueTask<IApprovalStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddJsonApprovalStore(
                new JsonApprovalStoreTarget(
                    Path.Combine(_directory, "approvals"),
                    new JsonApprovalStoreInstanceId(Guid.NewGuid()),
                    JsonStoreOpenMode.CreateIfMissing,
                    JsonStoreRecoveryMode.RecoverTornAppends),
                JsonApprovalStoreSettings.CreateDefault());
            _provider = services.BuildServiceProvider(validateScopes: true);
            _store = _provider.GetRequiredService<IApprovalStore>().ShouldBeOfType<JsonApprovalStore>();
            await _store.InitializeAsync(cancellationToken);
        }

        return _store;
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
