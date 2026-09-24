// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Marks one application-registered tool invoker and its immutable descriptor evidence at composition time.</summary>
/// <remarks>
/// Registration extensions add one marker per <c>AddToolInvoker</c> call. Markers are not resolved as services; they
/// supply metadata for <see cref="ApplicationToolProvider"/> discovery only.
/// </remarks>
public sealed record RegisteredToolInvoker
{
    /// <summary>Initializes one registered invoker marker.</summary>
    /// <param name="descriptor">The complete immutable tool descriptor published for discovery.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The descriptor identity or version is default.</exception>
    public RegisteredToolInvoker(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentOutOfRangeException.ThrowIfEqual(descriptor.Id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(descriptor.Version, default);
        Descriptor = descriptor;
        Identity = new ToolIdentity(descriptor.Id, descriptor.Version);
    }

    /// <summary>Gets the published descriptor.</summary>
    public ToolDescriptor Descriptor { get; }

    /// <summary>Gets the stable identity key used for keyed <see cref="IToolInvoker"/> resolution.</summary>
    public ToolIdentity Identity { get; }
}
