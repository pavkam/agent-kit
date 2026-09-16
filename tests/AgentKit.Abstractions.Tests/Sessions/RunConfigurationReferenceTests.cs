// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies RunConfigurationReference behavior and contracts.</summary>
public sealed class RunConfigurationReferenceTests
{
    [Fact]
    public void Constructor_WhenConfigurationVersionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunConfigurationReference(default, new RunPolicyVersion(1), new ContentHash("sha256:configuration")));
        exception.ParamName.ShouldBe("configurationVersion");
    }

    [Fact]
    public void Constructor_WhenPolicyVersionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunConfigurationReference(new ConfigurationVersion(1), default, new ContentHash("sha256:configuration")));
        exception.ParamName.ShouldBe("policyVersion");
    }

    [Fact]
    public void Constructor_WhenFingerprintIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), default));
        exception.ParamName.ShouldBe("fingerprint");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(2), new ContentHash("sha256:configuration"));
        reference.ConfigurationVersion.ShouldBe(new ConfigurationVersion(1));
        reference.PolicyVersion.ShouldBe(new RunPolicyVersion(2));
        reference.Fingerprint.ShouldBe(new ContentHash("sha256:configuration"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(2), new ContentHash("sha256:configuration"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
