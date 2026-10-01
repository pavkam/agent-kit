// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IProviderProfileRuntimeSelector"/> test double that returns a runtime lease over one fixed credential
/// source for the standard test binding, and counts selections and lease disposals.
/// </summary>
public sealed class StaticProviderProfileRuntimeSelector: IProviderProfileRuntimeSelector
{
    private readonly IProviderCredentialSource _source;
    private readonly Uri _baseAddress;
    private int _selections;
    private int _disposals;

    /// <summary>Initializes a new instance of the <see cref="StaticProviderProfileRuntimeSelector"/> class.</summary>
    /// <param name="source">The credential source every selected lease exposes.</param>
    /// <param name="baseAddress">The endpoint base address the selected endpoint snapshot reports, or null for a placeholder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public StaticProviderProfileRuntimeSelector(IProviderCredentialSource source, Uri? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _baseAddress = baseAddress ?? new Uri("https://selected.example.test/");
    }

    /// <summary>Gets the credential source key every test source and snapshot uses.</summary>
    public static ProviderCredentialSourceKey SourceKey { get; } = new("test.credentials");

    /// <summary>Gets the endpoint profile reference of the standard test binding.</summary>
    public static ProviderEndpointProfileReference Endpoint { get; } =
        new(new ProviderEndpointProfileKey("test.endpoint"), new ProviderEndpointProfileVersion(1));

    /// <summary>Gets the credential profile reference of the standard test binding.</summary>
    public static ProviderCredentialProfileReference Credential { get; } =
        new(new ProviderCredentialProfileKey("test.credential"), new ProviderCredentialProfileVersion(1));

    /// <summary>Gets the standard test binding bound descriptors carry.</summary>
    public static ProviderOperationBinding Binding { get; } = new(Endpoint, Credential);

    /// <summary>Gets how many runtime leases were selected.</summary>
    public int Selections => Volatile.Read(ref _selections);

    /// <summary>Gets how many selected runtime leases were disposed.</summary>
    public int Disposals => Volatile.Read(ref _disposals);

    /// <inheritdoc/>
    public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
        ProviderOperationBinding binding,
        ProtectedSemanticOperationContext operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _selections);

        return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(new ProviderProfileRuntimeSelected(CreateLease(binding)));
    }

    /// <summary>Creates a runtime lease over the selector's fixed source for <paramref name="binding"/> without counting a selection.</summary>
    /// <param name="binding">The endpoint and credential profile references the lease snapshots carry.</param>
    /// <returns>A lease whose disposal increments <see cref="Disposals"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    public IProviderProfileRuntimeLease CreateLease(ProviderOperationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var provider = new ProviderId("test-provider");
        var surface = new ProviderServiceSurfaceId("test-surface");
        var endpoint = new ProviderEndpointProfileSnapshot(
            binding.Endpoint,
            provider,
            surface,
            new ProviderEndpointId("test-endpoint"),
            _baseAddress,
            apiVersion: null,
            new ContentHash("endpoint:test"),
            ExtensionData.Empty);
        var credential = new ProviderCredentialProfileSnapshot(
            binding.Credential,
            provider,
            surface,
            _source.Key,
            accountId: null,
            TimeSpan.Zero,
            new ContentHash("credential:test"),
            ExtensionData.Empty);
        return new Lease(endpoint, credential, _source, this);
    }

    private void Disposed() => _ = Interlocked.Increment(ref _disposals);

    private sealed class Lease(
        ProviderEndpointProfileSnapshot endpoint,
        ProviderCredentialProfileSnapshot credential,
        IProviderCredentialSource source,
        StaticProviderProfileRuntimeSelector owner): IProviderProfileRuntimeLease
    {
        public ProviderEndpointProfileSnapshot Endpoint { get; } = endpoint;

        public ProviderCredentialProfileSnapshot Credential { get; } = credential;

        public IProviderCredentialSource CredentialSource { get; } = source;

        public ValueTask DisposeAsync()
        {
            owner.Disposed();
            return ValueTask.CompletedTask;
        }
    }
}
