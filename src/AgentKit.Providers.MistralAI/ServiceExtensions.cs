// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Mistral AI conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddMistralAI"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddMistralAIApiKeyCredential</c> or
/// <c>AddMistralAIOAuthCredential</c> configures authentication, and
/// <c>AddMistralAILlmModel</c> is called once per named model an
/// application wants to use. No default fabricates an API key, endpoint, or
/// model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Mistral AI endpoint and wire-behavior options,
        /// along with the default request translator, response parser,
        /// default <see cref="TimeProvider"/>, and a dedicated
        /// <see cref="HttpClient"/>.
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
        /// <c>AddMistralAIApiKeyCredential</c>,
        /// <c>AddMistralAIOAuthCredential</c>, and
        /// <c>AddMistralAILlmModel</c>.
        /// </remarks>
        public IServiceCollection AddMistralAI(Action<MistralAIProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOptions<MistralAIProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<MistralAIProviderOptions>, MistralAIProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton<IMistralAIRequestTranslator, MistralAIRequestTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IMistralAIResponseParser, MistralAIResponseParser>();
            services.TryAddSingleton<IMistralAIEmbeddingRequestTranslator, MistralAIEmbeddingRequestTranslator>();
            services.TryAddSingleton<IMistralAIEmbeddingResponseParser, MistralAIEmbeddingResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered
            // (non-streaming) attempt regardless of the caller's LlmModelRequest.Deadline, since
            // this adapter's own deadlineSource is layered on top of, not instead of, the
            // transport-level timeout. The per-request deadlineSource already bounds every attempt.
            services.TryAddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

            return services;
        }

        /// <summary>
        /// Registers a static Mistral AI API key as the credential source
        /// for every Mistral AI chat model, sent as an
        /// <c>Authorization: Bearer</c> header.
        /// </summary>
        /// <param name="apiKey">The non-empty Mistral AI API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Mistral AI provider key:
        /// it uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Mistral AI credential source an application (or
        /// <c>AddMistralAIOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddMistralAIApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId, credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Mistral AI chat model, sent through the
        /// same <c>Authorization: Bearer</c> header shape as a static API
        /// key.
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
        /// This registration is singular for the Mistral AI provider key:
        /// it uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Mistral AI credential source an application (or
        /// <c>AddMistralAIApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddMistralAIOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(MistralAIProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                MistralAIProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named Mistral AI chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Mistral's own model identifier, such as <c>"mistral-large-latest"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="MistralAIProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="MistralAIProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddMistralAI"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddMistralAILlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    MistralAIProviderDefaults.ProviderId,
                    MistralAIProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? MistralAIProviderDefaults.DefaultCapabilities,
                    limits ?? MistralAIProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new MistralAILlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IMistralAIRequestTranslator>(),
                    provider.GetRequiredService<IMistralAIResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one named Mistral AI embedding model as an additional
        /// <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Mistral's own embedding model identifier, such as <c>"mistral-embed"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="MistralAIProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input/output limits, or <see langword="null"/> to use
        /// <see cref="MistralAIProviderDefaults.DefaultEmbeddingLimits"/>.
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
        /// <see cref="AddMistralAI"/> must be called first.
        /// </remarks>
        public IServiceCollection AddMistralAIEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<MistralAIProviderOptions>>().Value;

                var descriptor = new EmbeddingModelDescriptor(
                    alias,
                    MistralAIProviderDefaults.ProviderId,
                    MistralAIProviderDefaults.EmbeddingApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? MistralAIProviderDefaults.DefaultEmbeddingCapabilities,
                    limits ?? MistralAIProviderDefaults.DefaultEmbeddingLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new MistralAIEmbeddingModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IMistralAIEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<IMistralAIEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(MistralAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
