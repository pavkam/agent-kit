// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for sandbox provider selection outcomes.</summary>
public abstract record ProcessSandboxSelectionResult
{
    /// <summary>Initializes the base selection outcome.</summary>
    private protected ProcessSandboxSelectionResult()
    {
    }
}

/// <summary>One registered sandbox provider was selected.</summary>
public sealed record ProcessSandboxSelected: ProcessSandboxSelectionResult
{
    /// <summary>Initializes a successful selection.</summary>
    /// <param name="profileId">The selected profile identity.</param>
    /// <param name="provider">The provider registered for the profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is invalid.</exception>
    public ProcessSandboxSelected(SandboxProfileId profileId, IProcessSandboxProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ArgumentNullException.ThrowIfNull(provider);
        ProfileId = profileId;
        Provider = provider;
    }

    /// <summary>Gets the selected profile identity.</summary>
    public SandboxProfileId ProfileId { get; init; }

    /// <summary>Gets the provider registered for the profile.</summary>
    public IProcessSandboxProvider Provider { get; init; }
}

/// <summary>No sandbox provider is registered for the requested profile identity.</summary>
/// <param name="ProfileId">The missing profile identity.</param>
public sealed record ProcessSandboxMissing(SandboxProfileId ProfileId): ProcessSandboxSelectionResult;
