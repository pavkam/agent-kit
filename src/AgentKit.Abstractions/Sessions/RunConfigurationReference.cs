// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains the exact immutable run configuration and policy snapshot required by recovery.</summary>
/// <remarks>Recovery resolves this reference exactly and reports typed unavailability or corruption when the retained version or fingerprint cannot be restored; it never substitutes current configuration.</remarks>
public sealed record RunConfigurationReference
{
    /// <summary>Initializes a retained run-configuration reference.</summary><param name="configurationVersion">The positive effective configuration revision.</param><param name="policyVersion">The positive run-policy revision.</param><param name="fingerprint">The canonical non-default snapshot fingerprint.</param><exception cref="ArgumentOutOfRangeException">A version or fingerprint is default.</exception>
    public RunConfigurationReference(ConfigurationVersion configurationVersion, RunPolicyVersion policyVersion, ContentHash fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(configurationVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(policyVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(fingerprint, default);
        ConfigurationVersion = configurationVersion;
        PolicyVersion = policyVersion;
        Fingerprint = fingerprint;
    }
    /// <summary>Gets the effective configuration revision.</summary><value>The positive retained revision.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the run-policy revision.</summary><value>The positive retained policy revision.</value>
    public RunPolicyVersion PolicyVersion { get; }
    /// <summary>Gets the snapshot fingerprint.</summary><value>The canonical digest used to reject mismatched retained material.</value>
    public ContentHash Fingerprint { get; }
}
