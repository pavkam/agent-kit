// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Configures the pre-admission bounds and preprocessing evidence used by the first-party input coordinator.</summary>
/// <remarks>
/// <para>
/// This is a mutable options type bound through <see cref="IOptions{TOptions}"/>. Hosts configure it
/// with the <c>configure</c> delegate of <c>AddInputCoordinator</c> on <see cref="ServiceExtensions"/> or with
/// <c>services.Configure&lt;InputCoordinatorOptions&gt;(...)</c>; the registration validates the bound values eagerly at host
/// start and again when the coordinator is resolved, so a property left in an invalid state fails at composition rather than mid-run.
/// </para>
/// <para>
/// These are the coordinator's own mechanics. Queue capacity, retention, and per-principal limits belong to the selected queue and
/// its durable store.
/// </para>
/// </remarks>
public sealed class InputCoordinatorOptions
{
    /// <summary>Initializes the options with this package's documented defaults so the options pattern can bind and configure them.</summary>
    public InputCoordinatorOptions()
    {
    }

    /// <summary>Initializes and eagerly validates the options as a convenience for code that constructs them directly.</summary>
    /// <remarks>
    /// This constructor predates the options-pattern binding and is retained so existing call sites keep compiling. It validates its
    /// arguments immediately; the properties it assigns remain settable afterwards, and values assigned later are validated only by the
    /// registration in <see cref="ServiceExtensions"/> and by the coordinator constructor.
    /// </remarks>
    /// <param name="preprocessingConfigurationVersion">The revision recorded in every preprocessing manifest this coordinator produces, defaulting to the first revision. The first-party coordinator applies no preprocessing, so hosts that later configure preprocessors must advance this revision.</param>
    /// <param name="maximumInputParts">The maximum content parts accepted in one payload, defaulting to 256.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumInputParts"/> is less than one, or a supplied <paramref name="preprocessingConfigurationVersion"/> is default.</exception>
    public InputCoordinatorOptions(ConfigurationVersion? preprocessingConfigurationVersion = null, int maximumInputParts = 256)
    {
        if (preprocessingConfigurationVersion is { } revision)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(revision, default, nameof(preprocessingConfigurationVersion));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInputParts, 1);
        PreprocessingConfigurationVersion = preprocessingConfigurationVersion ?? new ConfigurationVersion(1);
        MaximumInputParts = maximumInputParts;
    }

    /// <summary>Gets or sets the preprocessing configuration revision recorded with each admission.</summary>
    /// <value>A non-default revision, defaulting to the first revision; equivalent replay of an earlier admission keeps the revision captured at that time rather than this one. The first-party coordinator applies no preprocessing, so hosts that later configure preprocessors must advance this revision.</value>
    public ConfigurationVersion PreprocessingConfigurationVersion { get; set; } = new(1);

    /// <summary>Gets or sets the maximum content parts accepted in one payload.</summary>
    /// <value>A positive count, defaulting to 256, checked before any durable append. This bounds part count only; payload byte bounds remain a separate unimplemented responsibility.</value>
    public int MaximumInputParts { get; set; } = 256;
}
