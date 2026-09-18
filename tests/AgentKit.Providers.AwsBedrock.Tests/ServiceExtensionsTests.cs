// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies the <c>AddAwsBedrock*</c> dependency-injection registration
/// surface: region options, credential source selection, and additive
/// model registration.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAwsBedrock_WhenRegionConfigured_RegistersOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AwsBedrockProviderOptions>>().Value;

        options.Region.ShouldBe("us-east-1");
        options.PreferStreaming.ShouldBeTrue();
    }

    [Fact]
    public void AddAwsBedrock_WhenRegistered_DisablesTheHttpClientTimeoutInFavorOfThePerRequestDeadline()
    {
        // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered Converse
        // attempt regardless of the caller's LlmModelRequest.Deadline, since this adapter's own
        // deadlineSource is layered on top of, not instead of, the transport-level timeout.
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HttpClient>();

        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void AddAwsBedrock_WhenRegionNotConfigured_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(_ => { });

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AwsBedrockProviderOptions>>().Value);
    }

    [Fact]
    public void AddAwsBedrockStaticCredential_WhenRegistered_ResolvesStaticCredentialSource()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");
        _ = services.AddAwsBedrockStaticCredential("AKIAEXAMPLE", "secret");

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IAwsCredentialSource>(AwsBedrockProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticAwsCredentialSource>();
    }

    [Fact]
    public void AddAwsBedrockCredentialSource_WhenRegistered_ResolvesCustomSource()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");
        _ = services.AddAwsBedrockCredentialSource<StaticCredentialSourceRegistration>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredKeyedService<IAwsCredentialSource>(AwsBedrockProviderDefaults.ProviderId);

        _ = source.ShouldBeOfType<StaticCredentialSourceRegistration>();
    }

    [Fact]
    public void AddAwsBedrockLlmModel_WhenCalledMultipleTimes_RegistersAdditiveModels()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");
        _ = services.AddAwsBedrockStaticCredential("AKIAEXAMPLE", "secret");
        _ = services.AddAwsBedrockLlmModel(new ModelAlias("fast"), new ModelId("anthropic.claude-3-haiku-20240307-v1:0"));
        _ = services.AddAwsBedrockLlmModel(new ModelAlias("smart"), new ModelId("anthropic.claude-3-sonnet-20240229-v1:0"));

        using var provider = services.BuildServiceProvider();
        var models = provider.GetServices<ILlmModel>().ToArray();

        models.Length.ShouldBe(2);
        models.Select(m => m.Alias.Value).ShouldBe(["fast", "smart"], ignoreOrder: true);
    }

    [Fact]
    public void AddAwsBedrockLlmModel_WhenResolved_UsesAwsBedrockLlmModelType()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");
        _ = services.AddAwsBedrockStaticCredential("AKIAEXAMPLE", "secret");
        _ = services.AddAwsBedrockLlmModel(new ModelAlias("chat"), new ModelId("anthropic.claude-3-sonnet-20240229-v1:0"));

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<AwsBedrockLlmModel>();

        model.Alias.ShouldBe(new ModelAlias("chat"));
    }

    [Fact]
    public void AddAwsBedrockLlmModel_WhenGivenADescriptor_RegistersAnAdapterServingThatExactDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddAwsBedrock(options => options.Region = "us-east-1");
        _ = services.AddAwsBedrockStaticCredential("AKIAEXAMPLE", "secret");
        var descriptor = new ModelDescriptor(
            new ModelAlias("exact"),
            AwsBedrockProviderDefaults.ProviderId,
            AwsBedrockProviderDefaults.ApiFamily,
            new ModelId("anthropic.claude-3-haiku-20240307-v1:0"),
            deploymentId: new DeploymentId("test-deployment"),
            AwsBedrockProviderDefaults.DefaultCapabilities,
            AwsBedrockProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        _ = services.AddAwsBedrockLlmModel(descriptor);

        using var provider = services.BuildServiceProvider();
        var model = provider.GetRequiredService<ILlmModel>().ShouldBeOfType<AwsBedrockLlmModel>();
        model.Alias.ShouldBe(descriptor.Alias);
    }

    [Fact]
    public void AddAwsBedrockLlmModel_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddAwsBedrockLlmModel(null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void AddAwsBedrockLlmModel_WhenDescriptorNamesAnotherProvider_ThrowsArgumentExceptionBeforeRegistering()
    {
        var services = new ServiceCollection();
        var foreign = new ModelDescriptor(
            new ModelAlias("foreign"),
            new ProviderId("someone-else"),
            AwsBedrockProviderDefaults.ApiFamily,
            new ModelId("anthropic.claude-3-haiku-20240307-v1:0"),
            deploymentId: new DeploymentId("test-deployment"),
            AwsBedrockProviderDefaults.DefaultCapabilities,
            AwsBedrockProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => services.AddAwsBedrockLlmModel(foreign));

        exception.ParamName.ShouldBe("descriptor");
        services.ShouldBeEmpty();
    }

    private sealed class StaticCredentialSourceRegistration: IAwsCredentialSource
    {
        public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AwsSigV4Credential("AKIAEXAMPLE", "secret", null));
    }
}
