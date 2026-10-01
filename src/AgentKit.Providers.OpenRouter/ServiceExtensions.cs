// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using AgentKit.Providers;
using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the OpenRouter conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddOpenRouter"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddOpenRouterApiKeyCredential</c> or
/// <c>AddOpenRouterOAuthCredential</c> configures authentication, and
/// <c>AddOpenRouterLlmModel</c> is called once per named model an
/// application wants to route through OpenRouter. No default fabricates an
/// API key, endpoint, or model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the OpenRouter endpoint and wire-behavior options,
        /// along with the shared OpenAI-compatible request translator,
        /// response parser, default <see cref="TimeProvider"/>. Adapters built from
        /// these registrations send through <see cref="ProviderEgress"/> and own no HTTP client.
        /// </summary>
        /// <param name="configureOptions">An optional callback that overrides the default options.</param>
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
        /// <c>AddOpenRouterApiKeyCredential</c>,
        /// <c>AddOpenRouterOAuthCredential</c>, and
        /// <c>AddOpenRouterLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddOpenRouter(Action<OpenRouterProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<OpenRouterProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<OpenRouterProviderOptions>, OpenRouterProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }


            _ = OpenAICompatibleProviderProfileRegistration.RegisterChatProfiles(
                services,
                OpenRouterProviderDefaults.ProviderId,
                OpenRouterProviderDefaults.ChatServiceSurface,
                OpenRouterProviderDefaults.DefaultBaseAddress,
                OpenRouterProviderDefaults.CredentialSourceKey,
                OpenRouterProviderDefaults.ChatEndpointProfileKey,
                OpenRouterProviderDefaults.ChatCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);

            _ = OpenAICompatibleProviderProfileRegistration.RegisterEmbeddingProfiles(
                services,
                OpenRouterProviderDefaults.ProviderId,
                OpenRouterProviderDefaults.EmbeddingServiceSurface,
                OpenRouterProviderDefaults.DefaultBaseAddress,
                OpenRouterProviderDefaults.CredentialSourceKey,
                OpenRouterProviderDefaults.EmbeddingEndpointProfileKey,
                OpenRouterProviderDefaults.EmbeddingCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);

            services.TryAddSingleton<IOpenRouterRerankRequestTranslator, OpenRouterRerankRequestTranslator>();
            services.TryAddSingleton<IOpenRouterRerankResponseParser, OpenRouterRerankResponseParser>();

            _ = ProviderOperationProfileRegistration.RegisterDefaultOperationProfiles(
                services,
                OpenRouterProviderDefaults.ProviderId,
                OpenRouterProviderDefaults.RerankServiceSurface,
                OpenRouterProviderDefaults.DefaultBaseAddress,
                OpenRouterProviderDefaults.CredentialSourceKey,
                OpenRouterProviderDefaults.RerankEndpointProfileKey,
                OpenRouterProviderDefaults.RerankCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);

            services.TryAddSingleton(TimeProvider.System);

            return services;
        }

        /// <summary>
        /// Registers a static OpenRouter API key as the credential source
        /// for every OpenRouter chat model.
        /// </summary>
        /// <param name="apiKey">The non-empty OpenRouter API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the OpenRouter provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// OpenRouter credential source an application (or
        /// <c>AddOpenRouterOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddOpenRouterApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            _ = OpenAICompatibleProviderProfileRegistration.RegisterDualKeyCredentialSource(
                services,
                OpenRouterProviderDefaults.CredentialSourceKey,
                OpenRouterProviderDefaults.ProviderId,
                credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every OpenRouter chat model.
        /// </summary>
        /// <typeparam name="TProvider">
        /// The application-owned implementation that supplies and refreshes
        /// the current OAuth access token.
        /// </typeparam>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the OpenRouter provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// OpenRouter credential source an application (or
        /// <c>AddOpenRouterApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddOpenRouterOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(
                OpenRouterProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                OpenRouterProviderDefaults.CredentialSourceKey,
                static (provider, _) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(OpenRouterProviderDefaults.ProviderId)));
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                OpenRouterProviderDefaults.ProviderId,
                static (provider, _) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(OpenRouterProviderDefaults.ProviderId)));

            return services;
        }

        /// <summary>
        /// Registers one named, OpenRouter-routed chat model as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">
        /// OpenRouter's model slug, such as <c>"openai/gpt-4o"</c> or
        /// <c>"anthropic/claude-3.5-sonnet"</c>.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="OpenRouterProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="OpenRouterProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddOpenRouter"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddOpenRouterLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddOpenRouterLlmModel(new ModelDescriptor(
                alias,
                OpenRouterProviderDefaults.ProviderId,
                OpenRouterProviderDefaults.ApiFamily,
                modelId,
                deploymentId: null,
                capabilities ?? OpenRouterProviderDefaults.DefaultCapabilities,
                limits ?? OpenRouterProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty));
        }

        /// <summary>
        /// Registers one OpenRouter conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">
        /// The exact descriptor the adapter will serve; it must name the OpenRouter provider and API family.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was registered with, so
        /// publish this same instance to the catalog (for example through <c>AddModelDescriptors</c>) rather than
        /// rebuilding an equivalent one. Additive; <see cref="AddOpenRouter"/> must be called first.
        /// </remarks>
        public IServiceCollection AddOpenRouterLlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != OpenRouterProviderDefaults.ProviderId || descriptor.ApiFamily != OpenRouterProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the OpenRouter provider and API family.", nameof(descriptor));
            }

            var boundDescriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                descriptor,
                OpenRouterProviderDefaults.ChatServiceSurface,
                OpenRouterProviderDefaults.ChatEndpointProfileKey,
                OpenRouterProviderDefaults.ChatCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;

                return new OpenRouterLlmModel(
                    boundDescriptor,
                    OpenRouterProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId),
                    provider.GetRequiredService<ProviderEgress>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetService<IProviderProfileRuntimeSelector>());
            });

            return services;
        }

        /// <summary>
        /// Registers one named, OpenRouter-routed embedding model as an
        /// additional <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">
        /// OpenRouter's embedding model slug, such as
        /// <c>"openai/text-embedding-3-small"</c>.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="OpenRouterProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input/output limits, or <see langword="null"/> to use
        /// <see cref="OpenRouterProviderDefaults.DefaultEmbeddingLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;IEmbeddingModel&gt;</c>.
        /// <see cref="AddOpenRouter"/> must be called first.
        /// </remarks>
        public IServiceCollection AddOpenRouterEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;

                var descriptor = ProviderOperationDescriptorBinding.ApplyEmbeddingBinding(
                    new EmbeddingModelDescriptor(
                        alias,
                        OpenRouterProviderDefaults.ProviderId,
                        OpenRouterProviderDefaults.EmbeddingApiFamily,
                        modelId,
                        deploymentId: null,
                        capabilities ?? OpenRouterProviderDefaults.DefaultEmbeddingCapabilities,
                        limits ?? OpenRouterProviderDefaults.DefaultEmbeddingLimits,
                        pricing: null,
                        ExtensionData.Empty),
                    OpenRouterProviderDefaults.EmbeddingServiceSurface,
                    OpenRouterProviderDefaults.EmbeddingEndpointProfileKey,
                    OpenRouterProviderDefaults.EmbeddingCredentialProfileKey,
                    OpenRouterProviderDefaults.DefaultEndpointId);

                return new OpenRouterEmbeddingModel(
                    descriptor,
                    OpenRouterProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId),
                    provider.GetRequiredService<ProviderEgress>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetService<IProviderProfileRuntimeSelector>());
            });

            return services;
        }

        /// <summary>Registers one named OpenRouter reranker as an additional <see cref="IReranker"/> implementation.</summary>
        /// <param name="alias">The application-facing selection key.</param>
        /// <param name="modelId">The OpenRouter rerank model slug.</param>
        /// <param name="capabilities">Optional capability overrides.</param>
        /// <param name="limits">Optional limit overrides.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        public IServiceCollection AddOpenRouterReranker(
            RerankerAlias alias,
            ModelId modelId,
            RerankerCapabilities? capabilities = null,
            RerankerLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var descriptor = ProviderOperationDescriptorBinding.ApplyRerankBinding(
                new RerankerDescriptor(
                    alias,
                    OpenRouterProviderDefaults.ProviderId,
                    OpenRouterProviderDefaults.RerankApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? OpenRouterProviderDefaults.DefaultRerankCapabilities,
                    limits ?? OpenRouterProviderDefaults.DefaultRerankLimits,
                    ExtensionData.Empty),
                OpenRouterProviderDefaults.RerankServiceSurface,
                OpenRouterProviderDefaults.RerankEndpointProfileKey,
                OpenRouterProviderDefaults.RerankCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);

            _ = services.AddSingleton<IReranker>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;
                return new OpenRouterReranker(
                    descriptor,
                    options,
                    provider.GetRequiredService<IOpenRouterRerankRequestTranslator>(),
                    provider.GetRequiredService<IOpenRouterRerankResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenRouterProviderDefaults.ProviderId),
                    provider.GetRequiredService<ProviderEgress>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetService<IProviderProfileRuntimeSelector>());
            });

            return services;
        }
    }
}
