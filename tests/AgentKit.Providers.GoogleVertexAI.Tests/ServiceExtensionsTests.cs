// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddGoogleVertexAI*</c> dependency-injection registration
/// surface: project/region options, credential source selection, and
/// additive model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    private static void ConfigureOptions(GoogleVertexAIProviderOptions options)
    {
        options.ProjectId = "my-project";
        options.Location = "us-central1";
    }

    [Fact]
    public void AddGoogleVertexAI_WhenProjectAndLocationConfigured_RegistersOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

        options.ProjectId.ShouldBe("my-project");
        options.Location.ShouldBe("us-central1");
        options.Publisher.ShouldBe(GoogleVertexAIProviderDefaults.DefaultPublisher);
        options.ApiVersion.ShouldBe(GoogleVertexAIProviderDefaults.DefaultApiVersion);
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddGoogleVertexAI_WhenProjectIdNotConfigured_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(options => options.Location = "us-central1");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value);
    }

    [Fact]
    public void AddGoogleVertexAI_WhenLocationNotConfigured_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(options => options.ProjectId = "my-project");

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value);
    }

    /// <summary>Verifies a relative BaseAddress fails options validation at startup rather than at first request.</summary>
    [Fact]
    public void AddGoogleVertexAI_WhenBaseAddressIsNotAbsolute_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(options =>
        {
            ConfigureOptions(options);
            options.BaseAddress = new Uri("not-absolute", UriKind.Relative);
        });

        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value);
        exception.Message.ShouldContain(nameof(GoogleVertexAIProviderOptions.BaseAddress));
    }

    [Fact]
    public void AddGoogleVertexAI_WhenBaseAddressIsAbsolute_RegistersOptionsWithBaseAddress()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(options =>
        {
            ConfigureOptions(options);
            options.BaseAddress = new Uri("https://vertex.psc.internal.example/");
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBe(new Uri("https://vertex.psc.internal.example/"));
    }

    [Fact]
    public void AddGoogleVertexAI_WhenBaseAddressNotConfigured_LeavesItNullSoLocationSelectsHost()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

        options.BaseAddress.ShouldBeNull();
    }

    [Fact]
    public void AddGoogleVertexAI_WhenLocationIsGlobal_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(options =>
        {
            options.ProjectId = "my-project";
            options.Location = "global";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

        options.Location.ShouldBe(GoogleVertexAIProviderDefaults.GlobalLocation);
    }

    [Fact]
    public void AddGoogleVertexAIOAuthCredential_WhenRegistered_ResolvesDelegatingOAuthSource()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleVertexAIProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<DelegatingOAuthCredentialSource>();
    }

    [Fact]
    public void AddGoogleVertexAILlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        _ = services.AddGoogleVertexAILlmModel(new ModelAlias("fast"), new ModelId("gemini-2.5-flash"));
        _ = services.AddGoogleVertexAILlmModel(new ModelAlias("smart"), new ModelId("gemini-2.5-pro"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddGoogleVertexAILlmModel_WhenResolved_UsesGoogleVertexAIProviderIdentity()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        _ = services.AddGoogleVertexAILlmModel(new ModelAlias("chat"), new ModelId("gemini-2.5-flash"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleVertexAILlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddGoogleVertexAIEmbeddingModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        _ = services.AddGoogleVertexAIEmbeddingModel(new EmbeddingModelAlias("small"), new ModelId("text-embedding-005"));
        _ = services.AddGoogleVertexAIEmbeddingModel(new EmbeddingModelAlias("gemini"), new ModelId("gemini-embedding-001"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<IEmbeddingModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["small", "gemini"], ignoreOrder: true);
    }

    [Fact]
    public void AddGoogleVertexAIEmbeddingModel_WhenResolved_UsesGoogleVertexAIEmbeddingModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        _ = services.AddGoogleVertexAIEmbeddingModel(new EmbeddingModelAlias("embed"), new ModelId("text-embedding-005"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<IEmbeddingModel>().ShouldBeOfType<GoogleVertexAIEmbeddingModel>();

        model.Alias.ShouldBe(new EmbeddingModelAlias("embed"));
    }

    [Fact]
    public void AddGoogleVertexAILlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddGoogleVertexAI(ConfigureOptions);
        _ = services.AddGoogleVertexAIOAuthCredential<StaticOAuthTokenProviderRegistration>();
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            GoogleVertexAIProviderDefaults.ProviderId,
            GoogleVertexAIProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: new DeploymentId("test-deployment"),
            GoogleVertexAIProviderDefaults.DefaultCapabilities,
            GoogleVertexAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddGoogleVertexAILlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<GoogleVertexAILlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddGoogleVertexAILlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddGoogleVertexAILlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddGoogleVertexAILlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            GoogleVertexAIProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: new DeploymentId("test-deployment"),
            GoogleVertexAIProviderDefaults.DefaultCapabilities,
            GoogleVertexAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddGoogleVertexAILlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProviderRegistration: IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential("token", null));
    }
}
