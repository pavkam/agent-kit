// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one exact published configuration source and its host-established classification.</summary>
/// <remarks>This value carries supplied source, fingerprint, layer, and trust evidence. It performs no discovery, hash verification, path compilation, or authority grant.</remarks>
public sealed record ConfigurationSourceReference
{
    /// <summary>Initializes immutable source publication evidence.</summary>
    /// <param name="sourceId">Nondefault source identity.</param>
    /// <param name="sourceVersion">Positive source revision.</param>
    /// <param name="layer">Defined configured layer.</param>
    /// <param name="trust">Defined host-established trust class.</param>
    /// <param name="fingerprint">Nondefault canonical content fingerprint.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/>, <paramref name="sourceVersion"/>, or <paramref name="fingerprint"/> is default, or <paramref name="layer"/> or <paramref name="trust"/> is undefined.</exception>
    public ConfigurationSourceReference(ConfigurationSourceId sourceId, ConfigurationSourceVersion sourceVersion, ConfigurationLayerKind layer, ConfigurationTrustClass trust, ContentHash fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceVersion, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(layer);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        ArgumentOutOfRangeException.ThrowIfEqual(fingerprint, default);
        SourceId = sourceId;
        SourceVersion = sourceVersion;
        Layer = layer;
        Trust = trust;
        Fingerprint = fingerprint;
    }
    /// <summary>Gets source identity.</summary>
    /// <value>A nondefault immutable source identity.</value>
    public ConfigurationSourceId SourceId { get; }
    /// <summary>Gets source publication revision.</summary>
    /// <value>A positive immutable revision.</value>
    public ConfigurationSourceVersion SourceVersion { get; }
    /// <summary>Gets configured source layer.</summary>
    /// <value>One defined layer.</value>
    public ConfigurationLayerKind Layer { get; }
    /// <summary>Gets host-established trust classification.</summary>
    /// <value>One defined trust class.</value>
    public ConfigurationTrustClass Trust { get; }
    /// <summary>Gets canonical source-content fingerprint.</summary>
    /// <value>A nondefault immutable fingerprint.</value>
    public ContentHash Fingerprint { get; }
}
