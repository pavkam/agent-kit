// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures bounded preflight evidence; it is not authority to skip evaluation.</summary>
public sealed record OutputSchemaPreflightManifest
{
    /// <summary>Initializes preflight evidence.</summary>
    /// <param name="profile">The non-null immutable profile that issued the evidence.</param>
    /// <param name="dialect">A dialect supported by <paramref name="profile"/>.</param>
    /// <param name="schemaFingerprint">The deterministic retained-schema fingerprint under that profile.</param>
    /// <param name="limits">The non-null processing limits observed during preflight.</param>
    /// <param name="observedDepth">The positive observed depth within <paramref name="limits"/>.</param>
    /// <param name="observedNodes">The positive observed node count within <paramref name="limits"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> or <paramref name="limits"/> is null, or a value-backed identity is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="dialect"/> is unsupported or <paramref name="schemaFingerprint"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An observed count is not positive or exceeds its corresponding limit.</exception>
    public OutputSchemaPreflightManifest(OutputSchemaEngineProfile profile, JsonSchemaDialectId dialect, ContentHash schemaFingerprint, OutputSchemaProcessingLimits limits, int observedDepth, int observedNodes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfUnsupportedOutputSchemaDialect(profile, dialect);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFingerprint.Value, nameof(schemaFingerprint));
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observedDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observedNodes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(observedDepth, limits.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(observedNodes, limits.MaximumNodes);

        Profile = profile;
        Dialect = dialect;
        SchemaFingerprint = schemaFingerprint;
        Limits = limits;
        ObservedDepth = observedDepth;
        ObservedNodes = observedNodes;
    }
    /// <summary>Gets issuing profile evidence.</summary><value>An immutable profile descriptor.</value>
    public OutputSchemaEngineProfile Profile { get; }
    /// <summary>Gets the preflight dialect.</summary><value>A profile-supported dialect.</value>
    public JsonSchemaDialectId Dialect { get; }
    /// <summary>Gets deterministic retained-schema fingerprint evidence.</summary><value>Evidence only; evaluation revalidates.</value>
    public ContentHash SchemaFingerprint { get; }
    /// <summary>Gets preflight bounds.</summary><value>Immutable processing limits.</value>
    public OutputSchemaProcessingLimits Limits { get; }
    /// <summary>Gets observed nesting depth.</summary><value>A positive count within limits.</value>
    public int ObservedDepth { get; }
    /// <summary>Gets observed node count.</summary><value>A positive count within limits.</value>
    public int ObservedNodes { get; }
}
