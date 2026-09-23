// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Frozen;

/// <summary>Selects sandbox providers registered by stable profile identity.</summary>
public sealed class DefaultProcessSandboxSelector: IProcessSandboxSelector
{
    private readonly FrozenDictionary<SandboxProfileId, IProcessSandboxProvider> _providers;

    /// <summary>Initializes the selector from additive sandbox provider registrations.</summary>
    /// <param name="providers">The registered sandbox providers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> is null.</exception>
    /// <exception cref="ArgumentException">Sandbox profile identities collide.</exception>
    public DefaultProcessSandboxSelector(IEnumerable<IProcessSandboxProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        try
        {
            _providers = providers.ToFrozenDictionary(static provider => provider.Descriptor.ProfileId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Sandbox profile identities must be unique.", nameof(providers), exception);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ProcessSandboxSelectionResult> SelectAsync(
        SandboxProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        cancellationToken.ThrowIfCancellationRequested();
        return _providers.TryGetValue(profileId, out var provider)
            ? ValueTask.FromResult<ProcessSandboxSelectionResult>(new ProcessSandboxSelected(profileId, provider))
            : ValueTask.FromResult<ProcessSandboxSelectionResult>(new ProcessSandboxMissing(profileId));
    }
}
