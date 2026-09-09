// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Captures portable compatibility evidence for one configured provider operation.
/// </summary>
/// <remarks>
/// This snapshot retains candidate, usage-phase, and schema-dialect claims needed
/// by provider-neutral preflight. It does not duplicate <see cref="ModelCapabilities"/>,
/// activate a concrete wire profile, validate a destination or fingerprint, or grant authority.
/// </remarks>
public sealed record CompatibilityProfile
{
    /// <summary>
    /// Initializes immutable portable compatibility evidence.
    /// </summary>
    /// <param name="key">The nondefault profile key.</param>
    /// <param name="version">The positive exact profile publication revision.</param>
    /// <param name="fingerprint">The nondefault retained publication fingerprint.</param>
    /// <param name="requestMultiplicity">The defined configured request candidate multiplicity.</param>
    /// <param name="responseMultiplicity">The defined configured response candidate multiplicity.</param>
    /// <param name="usageReporting">The defined available provider usage-report phases.</param>
    /// <param name="supportedToolSchemaDialects">
    /// Initialized unique supported tool-schema dialects; empty is valid for operations with no tool dialects.
    /// </param>
    /// <param name="extensions">The non-null immutable compatible evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or dialect is default, or an enum is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="supportedToolSchemaDialects"/> is default or contains duplicates.</exception>
    public CompatibilityProfile(
        CompatibilityProfileKey key,
        CompatibilityProfileVersion version,
        ContentHash fingerprint,
        ModelCandidateMultiplicity requestMultiplicity,
        ModelCandidateMultiplicity responseMultiplicity,
        ModelUsageReportingMode usageReporting,
        ImmutableArray<JsonSchemaDialectId> supportedToolSchemaDialects,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentOutOfRangeException.ThrowIfEqual(fingerprint, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(requestMultiplicity);
        ArgumentOutOfRangeException.ThrowIfUndefined(responseMultiplicity);
        ArgumentOutOfRangeException.ThrowIfUndefined(usageReporting);
        ArgumentException.ThrowIfDefault(supportedToolSchemaDialects);

        var dialects = new HashSet<JsonSchemaDialectId>();
        foreach (var dialect in supportedToolSchemaDialects)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(dialect, default, nameof(supportedToolSchemaDialects));
            ArgumentException.ThrowIfNotEqual(dialects.Add(dialect), true, nameof(supportedToolSchemaDialects));
        }

        ArgumentNullException.ThrowIfNull(extensions);

        Key = key;
        Version = version;
        Fingerprint = fingerprint;
        RequestMultiplicity = requestMultiplicity;
        ResponseMultiplicity = responseMultiplicity;
        UsageReporting = usageReporting;
        SupportedToolSchemaDialects = supportedToolSchemaDialects;
        Extensions = extensions;
    }

    /// <summary>Gets the profile publication key.</summary>
    /// <value>A nondefault exact key.</value>
    public CompatibilityProfileKey Key { get; }

    /// <summary>Gets the exact profile publication revision.</summary>
    /// <value>A positive version.</value>
    public CompatibilityProfileVersion Version { get; }

    /// <summary>Gets the retained profile fingerprint.</summary>
    /// <value>A nondefault hash retained without authenticity verification.</value>
    public ContentHash Fingerprint { get; }

    /// <summary>Gets the configured request candidate multiplicity.</summary>
    /// <value>One defined operation-contract value.</value>
    public ModelCandidateMultiplicity RequestMultiplicity { get; }

    /// <summary>Gets the configured response candidate multiplicity.</summary>
    /// <value>One defined operation-contract value.</value>
    public ModelCandidateMultiplicity ResponseMultiplicity { get; }

    /// <summary>Gets available provider usage-report phases.</summary>
    /// <value>Availability only; missing usage is still not reported.</value>
    public ModelUsageReportingMode UsageReporting { get; }

    /// <summary>Gets supported tool-schema dialects in retained order.</summary>
    /// <value>An initialized unique immutable array, possibly empty.</value>
    public ImmutableArray<JsonSchemaDialectId> SupportedToolSchemaDialects { get; }

    /// <summary>Gets compatible immutable extension evidence.</summary>
    /// <value>A non-null immutable bag.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Determines structural profile equality.</summary>
    /// <param name="other">The profile to compare, or null.</param>
    /// <returns>True when every scalar and ordered dialect value is equal.</returns>
    public bool Equals(CompatibilityProfile? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && Fingerprint == other.Fingerprint
        && RequestMultiplicity == other.RequestMultiplicity
        && ResponseMultiplicity == other.ResponseMultiplicity
        && UsageReporting == other.UsageReporting
        && SupportedToolSchemaDialects.SequenceEqual(other.SupportedToolSchemaDialects)
        && Extensions == other.Extensions;

    /// <summary>Returns a structural equality-compatible hash.</summary>
    /// <returns>A hash over all scalar and ordered dialect values.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(Fingerprint);
        hash.Add(RequestMultiplicity);
        hash.Add(ResponseMultiplicity);
        hash.Add(UsageReporting);

        foreach (var dialect in SupportedToolSchemaDialects)
        {
            hash.Add(dialect);
        }

        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
