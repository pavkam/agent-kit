// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Reads the configured revocation epoch from <see cref="AgentPermissionOptions"/>.</summary>
public sealed class DefaultSecurityRevocationGeneration: ISecurityRevocationGeneration
{
    private readonly IOptions<AgentPermissionOptions> _options;

    /// <summary>Initializes generation backed by permission options.</summary>
    /// <param name="options">The validated permission configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public DefaultSecurityRevocationGeneration(IOptions<AgentPermissionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityRevocationVersion> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = _options.Value.RevocationVersion;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return ValueTask.FromResult(new SecurityRevocationVersion(value));
    }
}
