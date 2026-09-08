// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no location is visible for an authorized session address.</summary>
/// <remarks>Directories use this same outcome for absent and tenant-masked locations so callers cannot distinguish session existence across an isolation boundary.</remarks>
public sealed record SessionLocationNotFound: SessionLocationResult
{
    /// <summary>Initializes a tenant-safe missing-location outcome.</summary>
    /// <param name="address">The non-null address that had no visible route.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public SessionLocationNotFound(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    /// <summary>Gets the requested address.</summary><value>The non-null address whose location was not visible.</value>
    public SessionAddress Address { get; }
}
