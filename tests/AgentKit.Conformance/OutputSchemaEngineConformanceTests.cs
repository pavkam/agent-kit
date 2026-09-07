// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies portable bounded preflight, evidence, evaluation, and cancellation behavior.</summary>
/// <typeparam name="TFixture">The independently composed implementation fixture.</typeparam>
public abstract class OutputSchemaEngineConformanceTests<TFixture>
    where TFixture : IOutputSchemaEngineConformanceFixture
{
    /// <summary>Creates a fresh fixture owned by one test case.</summary>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies repeated preflight produces stable captured evidence.</summary>
    [Fact]
    public void Preflight_WhenSchemaIsSupported_ReturnsRepeatableManifest()
    {
        using var fixture = CreateFixture();

        var first = fixture.Engine.Preflight(fixture.CreateSupportedPreflightRequest());
        var second = fixture.Engine.Preflight(fixture.CreateSupportedPreflightRequest());

        var firstManifest = first.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
        firstManifest.ShouldBe(second.ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest);
        firstManifest.Profile.ShouldBe(fixture.Engine.Profile);
        fixture.Engine.Profile.ShouldBe(firstManifest.Profile);
    }

    /// <summary>Verifies unsupported schema configuration fails closed with a typed result.</summary>
    [Fact]
    public void Preflight_WhenSchemaIsUnsupported_ReturnsConfigurationFailure()
    {
        using var fixture = CreateFixture();

        var result = fixture.Engine.Preflight(fixture.CreateUnsupportedPreflightRequest());

        _ = result.ShouldBeOfType<OutputSchemaPreflightRejected>().Failure;
    }

    /// <summary>Verifies a conforming candidate passes against captured evidence.</summary>
    [Fact]
    public void Evaluate_WhenCandidateIsValid_ReturnsPassed()
    {
        using var fixture = CreateFixture();
        var manifest = Preflight(fixture);

        var result = fixture.Engine.Evaluate(fixture.CreateValidEvaluationRequest(manifest));

        _ = result.ShouldBeOfType<OutputSchemaEvaluationPassed>();
    }

    /// <summary>Verifies a nonconforming candidate returns bounded issues.</summary>
    [Fact]
    public void Evaluate_WhenCandidateIsInvalid_ReturnsIssues()
    {
        using var fixture = CreateFixture();
        var manifest = Preflight(fixture);
        var request = fixture.CreateInvalidEvaluationRequest(manifest);

        var result = fixture.Engine.Evaluate(request);

        var issues = result.ShouldBeOfType<OutputSchemaCandidateInvalid>().Issues;
        issues.ShouldNotBeEmpty();
        issues.Length.ShouldBeLessThanOrEqualTo(request.MaximumIssues);
        fixture.Engine.Profile.ShouldBe(manifest.Profile);
    }

    /// <summary>Verifies changed preflight evidence cannot authorize evaluation.</summary>
    [Fact]
    public void Evaluate_WhenManifestFingerprintChanges_ReturnsConfigurationRejection()
    {
        using var fixture = CreateFixture();
        var manifest = Preflight(fixture);
        var forged = new OutputSchemaPreflightManifest(
            manifest.Profile, manifest.Dialect, new ContentHash("conformance:forged"), manifest.Limits,
            manifest.ObservedDepth, manifest.ObservedNodes);

        var result = fixture.Engine.Evaluate(fixture.CreateValidEvaluationRequest(forged));

        result.ShouldBeOfType<OutputSchemaEvaluationConfigurationRejected>().Failure.Kind
            .ShouldBe(OutputSchemaConfigurationFailureKind.PreflightEvidenceMismatch);
    }

    /// <summary>Verifies cancellation is honored before preflight work.</summary>
    [Fact]
    public void Preflight_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(() => fixture.Engine.Preflight(
            fixture.CreateSupportedPreflightRequest(), cancellation.Token));
    }

    /// <summary>Verifies cancellation is honored before evaluation work.</summary>
    [Fact]
    public void Evaluate_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var fixture = CreateFixture();
        var manifest = Preflight(fixture);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(() => fixture.Engine.Evaluate(
            fixture.CreateValidEvaluationRequest(manifest), cancellation.Token));
    }

    private static OutputSchemaPreflightManifest Preflight(TFixture fixture) =>
        fixture.Engine.Preflight(fixture.CreateSupportedPreflightRequest())
            .ShouldBeOfType<OutputSchemaPreflightAccepted>().Manifest;
}
