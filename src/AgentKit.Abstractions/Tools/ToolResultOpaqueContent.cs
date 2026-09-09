// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains unknown canonical typed content without CLR type activation or semantic promotion.</summary>
public sealed record ToolResultOpaqueContent: ToolResultContent
{
    /// <summary>Initializes opaque terminal content.</summary>
    /// <param name="typeDiscriminator">The nonblank stable wire discriminator.</param>
    /// <param name="canonicalPayload">The initialized owned canonical JSON bytes.</param>
    /// <param name="extensions">Compatible immutable field evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="typeDiscriminator"/> is blank or the payload is uninitialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolResultOpaqueContent(string typeDiscriminator, ExtensionValue canonicalPayload, ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeDiscriminator);
        ArgumentException.ThrowIfDefault(canonicalPayload.CanonicalJson, nameof(canonicalPayload));
        ArgumentNullException.ThrowIfNull(extensions);
        TypeDiscriminator = typeDiscriminator;
        CanonicalPayload = canonicalPayload;
        Extensions = extensions;
    }
    /// <summary>Gets the stable wire discriminator.</summary><value>Nonnull, nonblank text unrelated to CLR type names.</value>
    public string TypeDiscriminator { get; }
    /// <summary>Gets the owned canonical payload.</summary><value>Initialized immutable canonical JSON bytes.</value>
    public ExtensionValue CanonicalPayload { get; }
    /// <summary>Gets compatible field evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}
