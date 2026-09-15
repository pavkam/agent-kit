// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

/// <summary>Verifies <see cref="GoogleVertexAIProviderOptionsValidator"/> behavior and contracts.</summary>
public sealed class GoogleVertexAIProviderOptionsValidatorTests
{
    private static GoogleVertexAIProviderOptions CreateValidOptions() => new()
    {
        ProjectId = "my-project",
        Location = "us-central1",
    };

    [Fact]
    public void Validate_WhenProjectAndLocationConfigured_Succeeds() =>
        new GoogleVertexAIProviderOptionsValidator().Validate(name: null, CreateValidOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_WhenLocationIsGlobal_Succeeds()
    {
        var options = CreateValidOptions();
        options.Location = "global";

        new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenBaseAddressIsAbsolute_Succeeds()
    {
        var options = CreateValidOptions();
        options.BaseAddress = new Uri("https://vertex.psc.internal.example/");

        new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenBaseAddressIsRelative_Fails()
    {
        var options = CreateValidOptions();
        options.BaseAddress = new Uri("not-absolute", UriKind.Relative);

        var result = new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GoogleVertexAIProviderOptions.BaseAddress));
        result.FailureMessage.ShouldContain("absolute");
    }

    /// <summary>Verifies an explicit endpoint does not excuse a missing location, which still names the resource.</summary>
    [Fact]
    public void Validate_WhenBaseAddressSetButLocationMissing_Fails()
    {
        var options = new GoogleVertexAIProviderOptions
        {
            ProjectId = "my-project",
            BaseAddress = new Uri("https://vertex.psc.internal.example/"),
        };

        var result = new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GoogleVertexAIProviderOptions.Location));
    }

    [Fact]
    public void Validate_WhenProjectIdMissing_Fails()
    {
        var options = new GoogleVertexAIProviderOptions { Location = "us-central1" };

        var result = new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GoogleVertexAIProviderOptions.ProjectId));
    }

    [Fact]
    public void Validate_WhenPublisherIsWhiteSpace_Fails()
    {
        var options = CreateValidOptions();
        options.Publisher = " ";

        var result = new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GoogleVertexAIProviderOptions.Publisher));
    }

    [Fact]
    public void Validate_WhenApiVersionIsEmpty_Fails()
    {
        var options = CreateValidOptions();
        options.ApiVersion = string.Empty;

        var result = new GoogleVertexAIProviderOptionsValidator().Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GoogleVertexAIProviderOptions.ApiVersion));
    }

    [Fact]
    public void Validate_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new GoogleVertexAIProviderOptionsValidator().Validate(name: null, null!));

        exception.ParamName.ShouldBe("options");
    }
}
