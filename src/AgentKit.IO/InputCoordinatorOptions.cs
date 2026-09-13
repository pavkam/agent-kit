// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Captures the pre-admission bounds and preprocessing evidence used by the first-party input coordinator.</summary>
/// <remarks>These are the coordinator's own validated mechanics. Queue capacity, retention, and per-principal limits belong to the selected queue and its durable store.</remarks>
public sealed record InputCoordinatorOptions
{
    /// <summary>Captures validated coordinator mechanics before any admission is attempted.</summary>
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

    /// <summary>Gets the preprocessing configuration revision recorded with each admission.</summary>
    /// <value>A positive revision; equivalent replay of an earlier admission keeps the revision captured at that time rather than this one.</value>
    public ConfigurationVersion PreprocessingConfigurationVersion { get; }

    /// <summary>Gets the maximum content parts accepted in one payload.</summary>
    /// <value>A positive count checked before any durable append. This bounds part count only; payload byte bounds remain a separate unimplemented responsibility.</value>
    public int MaximumInputParts { get; }
}
