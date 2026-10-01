// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable model-choice policy an agent definition or run override
/// supplies to selection.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// Candidate order is meaningful and is preserved exactly as configured.
/// Selection never reorders candidates by inferred quality, price, or
/// popularity, because those judgements belong to whoever wrote the policy.
/// </para>
/// </remarks>
public sealed record ModelSelectionPolicy
{
    private readonly ImmutableArray<ModelAlias> _candidates;
    private readonly ExtensionData _extensions;
    private readonly ModelRequirements _requirements;
    private readonly LlmRequestSettings _requestSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSelectionPolicy"/>
    /// record.
    /// </summary>
    /// <param name="candidates">
    /// The ordered candidate aliases. At least one is required, since a
    /// policy with no candidates can never select anything.
    /// </param>
    /// <param name="fallback">
    /// Whether selection may move past the first candidate.
    /// </param>
    /// <param name="downgrade">
    /// What happens when a candidate cannot support a requirement.
    /// </param>
    /// <param name="extensions">
    /// Additional policy data for custom selectors. Defaults to
    /// <see cref="ExtensionData.Empty"/>.
    /// </param>
    /// <param name="requirements">
    /// The portable behaviors every request selected through this policy needs.
    /// Defaults to <see cref="ModelRequirements.None"/>.
    /// </param>
    /// <param name="requestSettings">
    /// The sampling and output settings applied to every request selected
    /// through this policy. Defaults to <see cref="LlmRequestSettings.Default"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="candidates"/> is uninitialized, empty, or contains a
    /// duplicate alias.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="candidates"/> contains a default alias, or
    /// <paramref name="fallback"/> or <paramref name="downgrade"/> is not a
    /// defined enumeration value.
    /// </exception>
    /// <remarks>
    /// <paramref name="requirements"/> and <paramref name="requestSettings"/> are
    /// a recorded deviation from the normative four-member policy shape: the
    /// agent definition has no member for either, so the model-choice policy
    /// that an <see cref="AgentDefinition"/> already carries is the one place
    /// that states which behaviors the agent's requests need and how they are
    /// sampled. See the composition-and-configuration architecture page.
    /// </remarks>
    public ModelSelectionPolicy(
        ImmutableArray<ModelAlias> candidates,
        ModelFallbackPolicy fallback = ModelFallbackPolicy.FirstCandidateOnly,
        CapabilityDowngradePolicy downgrade = CapabilityDowngradePolicy.Reject,
        ExtensionData? extensions = null,
        ModelRequirements? requirements = null,
        LlmRequestSettings? requestSettings = null)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(candidates);
        ThrowIfDuplicateCandidate(candidates, nameof(candidates));
        ArgumentOutOfRangeException.ThrowIfUndefined(fallback);
        ArgumentOutOfRangeException.ThrowIfUndefined(downgrade);

        _candidates = candidates;
        Fallback = fallback;
        Downgrade = downgrade;
        _extensions = extensions ?? ExtensionData.Empty;
        _requirements = requirements ?? ModelRequirements.None;
        _requestSettings = requestSettings ?? LlmRequestSettings.Default;
    }

    /// <summary>Gets the ordered candidate aliases.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized, empty, or duplicated
    /// candidate list.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to include a default alias.
    /// </exception>
    public ImmutableArray<ModelAlias> Candidates
    {
        get => _candidates;
        init
        {
            ArgumentException.ThrowIfDefaultOrEmpty(value, nameof(Candidates));
            ThrowIfDuplicateCandidate(value, nameof(Candidates));
            _candidates = value;
        }
    }

    /// <summary>Gets whether selection may move past the first candidate.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public ModelFallbackPolicy Fallback
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Fallback));
            field = value;
        }
    }

    /// <summary>
    /// Gets what happens when a candidate cannot support a requirement.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public CapabilityDowngradePolicy Downgrade
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Downgrade));
            field = value;
        }
    }

    /// <summary>Gets additional policy data for custom selectors.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ExtensionData Extensions
    {
        get => _extensions;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Extensions));
            _extensions = value;
        }
    }

    /// <summary>Gets the portable behaviors every request selected through this policy needs.</summary>
    /// <value>Used to reject or downgrade an incompatible candidate before any provider request is sent.</value>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelRequirements Requirements
    {
        get => _requirements;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Requirements));
            _requirements = value;
        }
    }

    /// <summary>Gets the sampling and output settings applied to every request selected through this policy.</summary>
    /// <value><see cref="LlmRequestSettings.Default"/> defers every setting to the provider.</value>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public LlmRequestSettings RequestSettings
    {
        get => _requestSettings;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RequestSettings));
            _requestSettings = value;
        }
    }

    /// <summary>Determines whether this policy has the same ordered declarative content as <paramref name="other"/>.</summary>
    /// <param name="other">The policy to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every policy field, including candidate order, is equal.</returns>
    public bool Equals(ModelSelectionPolicy? other) =>
        other is not null
        && Candidates.SequenceEqual(other.Candidates)
        && Fallback == other.Fallback
        && Downgrade == other.Downgrade
        && Extensions.Equals(other.Extensions)
        && Requirements.Equals(other.Requirements)
        && RequestSettings.Equals(other.RequestSettings);

    /// <summary>Returns a hash code consistent with structural policy equality.</summary>
    /// <returns>A hash code over each ordered candidate and every remaining policy field.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var candidate in Candidates)
        {
            hash.Add(candidate);
        }

        hash.Add(Fallback);
        hash.Add(Downgrade);
        hash.Add(Extensions);
        hash.Add(Requirements);
        hash.Add(RequestSettings);
        return hash.ToHashCode();
    }

    private static void ThrowIfDuplicateCandidate(
        ImmutableArray<ModelAlias> candidates,
        string paramName)
    {
        if (candidates.IsDefaultOrEmpty)
        {
            return;
        }

        var seen = new HashSet<ModelAlias>();
        foreach (var candidate in candidates)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(candidate, default, paramName);
            if (!seen.Add(candidate))
            {
                throw new ArgumentException(
                    $"Value must not contain duplicate candidate alias '{candidate}'.",
                    paramName);
            }
        }
    }
}
