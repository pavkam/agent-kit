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

    private sealed class StaticCredentialSourceRegistration: IAwsCredentialSource
    {
        public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AwsSigV4Credential("AKIAEXAMPLE", "secret", null));
    }
}
