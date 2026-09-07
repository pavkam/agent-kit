// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Azure OpenAI conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddAzureOpenAI"/> configures the
/// shared resource endpoint and wire-behavior options, exactly one of
/// <c>AddAzureOpenAIApiKeyCredential</c> or
/// <c>AddAzureOpenAIOAuthCredential</c> configures authentication, and
/// <c>AddAzureOpenAILlmModel</c> is called once per deployment an
/// application wants to use. No default fabricates a resource endpoint,
/// API key, or deployment an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Azure OpenAI endpoint and wire-behavior options,
        /// along with the shared OpenAI-compatible request translator,
        /// response parser, default <see cref="TimeProvider"/>, and a
        /// dedicated <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="configureOptions">
        /// Configures the options, most importantly
        /// <see cref="AzureOpenAIProviderOptions.ResourceEndpoint"/>, which
        /// has no default and must be set here.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This method must be called exactly once; calling it more than
        /// once applies <paramref name="configureOptions"/> more than once
        /// through the ordinary <c>Microsoft.Extensions.Options</c>
        /// configuration pipeline. Authentication and model registrations
        /// are independent calls documented on
        /// <c>AddAzureOpenAIApiKeyCredential</c>,
        /// <c>AddAzureOpenAIOAuthCredential</c>, and
        /// <c>AddAzureOpenAILlmModel</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="configureOptions"/> is null.</exception>
        public IServiceCollection AddAzureOpenAI(Action<AzureOpenAIProviderOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            _ = services.AddOptions<AzureOpenAIProviderOptions>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AzureOpenAIProviderOptions>, AzureOpenAIProviderOptionsValidator>());
            _ = services.Configure(configureOptions);

            services.TryAddSingleton<IOpenAIRequestTranslator, OpenAIRequestTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IOpenAIStreamParser, OpenAIChatCompletionResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

            return services;
        }

        /// <summary>
        /// Registers a static Azure OpenAI resource API key as the
        /// credential source for every Azure OpenAI chat model, sent as an
        /// <c>api-key</c> header.
        /// </summary>
        /// <param name="apiKey">The non-empty Azure OpenAI resource API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Azure OpenAI provider key:
        /// it uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// Azure OpenAI credential source an application (or
        /// <c>AddAzureOpenAIOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddAzureOpenAIApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(AzureOpenAIProviderDefaults.ProviderId, credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the Microsoft
        /// Entra access token source for every Azure OpenAI chat model,
        /// sent as an <c>Authorization: Bearer</c> header.
        /// </summary>
        /// <typeparam name="TProvider">
        /// The application-owned implementation that supplies and refreshes
        /// the current Entra access token, acquired for the
        /// <c>https://cognitiveservices.azure.com/.default</c> scope.
        /// </typeparam>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Azure OpenAI provider key:
        /// it uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// Azure OpenAI credential source an application (or
        /// <c>AddAzureOpenAIApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddAzureOpenAIOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(AzureOpenAIProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                AzureOpenAIProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one Azure OpenAI deployment as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">
        /// The underlying OpenAI model identity the deployment hosts, such
        /// as <c>"gpt-4o"</c>, used for AgentKit's own bookkeeping and
        /// selection.
        /// </param>
        /// <param name="deploymentId">
        /// The Azure deployment name, sent as the wire <c>model</c> field
        /// in place of <paramref name="modelId"/>.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="AzureOpenAIProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="AzureOpenAIProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional
        /// deployments alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddAzureOpenAI"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddAzureOpenAILlmModel(
            ModelAlias alias,
            ModelId modelId,
            DeploymentId deploymentId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AzureOpenAIProviderOptions>>().Value;
                var profile = AzureOpenAIProviderDefaults.CreateProfile(options);

                var descriptor = new ModelDescriptor(
                    alias,
                    AzureOpenAIProviderDefaults.ProviderId,
                    AzureOpenAIProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId,
                    capabilities ?? AzureOpenAIProviderDefaults.DefaultCapabilities,
                    limits ?? AzureOpenAIProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new AzureOpenAILlmModel(
                    descriptor,
                    profile,
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(AzureOpenAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
