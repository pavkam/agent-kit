// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that bounded schema preflight accepted a schema.</summary>
public sealed record OutputSchemaPreflightAccepted: OutputSchemaPreflightResult
{
    /// <summary>Initializes an accepted preflight result.</summary>
    /// <param name="manifest">The non-null captured preflight evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is <see langword="null"/>.</exception>
    public OutputSchemaPreflightAccepted(OutputSchemaPreflightManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Manifest = manifest;
    }

    /// <summary>Gets the evidence captured by schema preflight.</summary>
    /// <value>The immutable manifest that evaluation must revalidate.</value>
    public OutputSchemaPreflightManifest Manifest { get; }
}
