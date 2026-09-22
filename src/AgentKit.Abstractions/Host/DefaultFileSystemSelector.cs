// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Frozen;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Selects keyed file-system capabilities from explicit profile registrations.</summary>
public sealed class DefaultFileSystemSelector: IFileSystemSelector
{
    private readonly IServiceProvider _provider;
    private readonly FrozenDictionary<FileSystemProfileKey, FileSystemCapabilities> _capabilities;

    /// <summary>Initializes the selector from captured profile registrations.</summary>
    /// <param name="provider">The root service provider used to resolve keyed services.</param>
    /// <param name="profiles">The registered profile capability declarations.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DefaultFileSystemSelector(
        IServiceProvider provider,
        IEnumerable<FileSystemProfileRegistration> profiles)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(profiles);
        _provider = provider;
        _capabilities = profiles.ToFrozenDictionary(static profile => profile.Key, static profile => profile.Capabilities);
    }

    /// <inheritdoc/>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(requiredCapability);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_capabilities.TryGetValue(key, out var capabilities))
        {
            return ValueTask.FromResult<FileSystemSelectionResult>(new FileSystemProfileMissing(key));
        }

        if (!capabilities.Supported.HasFlag(requiredCapability))
        {
            return ValueTask.FromResult<FileSystemSelectionResult>(
                new FileSystemCapabilityUnsupported(key, requiredCapability, capabilities));
        }

        var serviceKey = key.Value;
        return ValueTask.FromResult<FileSystemSelectionResult>(requiredCapability switch
        {
            FileSystemCapability.Read => new FileSystemReaderSelected(
                key,
                _provider.GetRequiredKeyedService<IFileReader>(serviceKey),
                capabilities),
            FileSystemCapability.Write => new FileSystemWriterSelected(
                key,
                _provider.GetRequiredKeyedService<IFileWriter>(serviceKey),
                capabilities),
            FileSystemCapability.Metadata => new FileSystemMetadataReaderSelected(
                key,
                _provider.GetRequiredKeyedService<IFileMetadataReader>(serviceKey),
                capabilities),
            FileSystemCapability.CreateDirectory => new FileSystemDirectoryCreatorSelected(
                key,
                _provider.GetRequiredKeyedService<IDirectoryCreator>(serviceKey),
                capabilities),
            FileSystemCapability.None => new FileSystemCapabilityUnsupported(key, requiredCapability, capabilities),
            FileSystemCapability.Enumerate => new FileSystemDirectoryReaderSelected(
                key,
                _provider.GetRequiredKeyedService<IDirectoryReader>(serviceKey),
                capabilities),
            FileSystemCapability.Watch => new FileSystemCapabilityUnsupported(key, requiredCapability, capabilities),
            FileSystemCapability.Temporary => new FileSystemCapabilityUnsupported(key, requiredCapability, capabilities),
            _ => new FileSystemCapabilityUnsupported(key, requiredCapability, capabilities),
        });
    }
}
