// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationManifest behavior and contracts.</summary>
public sealed class OutputValidationManifestTests
{
    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputValidationManifest(new OutputDefinitionId("output"), (OutputMode) 99, 1, [])).ParamName.ShouldBe("mode");

    [Fact]
    public void Constructor_WhenValidationAttemptIsLessThanOne_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputValidationManifest(new OutputDefinitionId("output"), OutputMode.Text, 0, [])).ParamName.ShouldBe("validationAttempt");

    [Fact]
    public void Constructor_WhenIssuesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationManifest(new OutputDefinitionId("output"), OutputMode.Text, 1, default)).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var id = new OutputDefinitionId("output");
        var issue = OutputTestData.Issue();
        var manifest = new OutputValidationManifest(id, OutputMode.Text, 1, [issue]);
        manifest.DefinitionId.ShouldBe(id);
        manifest.Mode.ShouldBe(OutputMode.Text);
        manifest.ValidationAttempt.ShouldBe(1);
        manifest.Issues.ShouldBe([issue]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var id = new OutputDefinitionId("output");
        var left = new OutputValidationManifest(id, OutputMode.Text, 1, [OutputTestData.Issue()]);
        var right = new OutputValidationManifest(id, OutputMode.Text, 1, [OutputTestData.Issue()]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = OutputTestData.Manifest();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
