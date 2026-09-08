// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Google Gemini conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddGoogleGemini"/> configures
/// the shared endpoint and wire-behavior options, exactly one of
/// <c>AddGoogleGeminiApiKeyCredential</c> or
/// <c>AddGoogleGeminiOAuthCredential</c> configures authentication, and
/// <c>AddGoogleGeminiLlmModel</c> is called once per named model an
/// application wants to use. No default fabricates an API key, endpoint, or
/// model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Gemini endpoint and wire-behavior options, along
        /// with the default request translator, response parser, default
        /// <see cref="TimeProvider"/>, and a dedicated
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
        /// <c>AddGoogleGeminiApiKeyCredential</c>,
        /// <c>AddGoogleGeminiOAuthCredential</c>, and
        /// <c>AddGoogleGeminiLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddGoogleGemini(Action<GoogleGeminiProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOptions<GoogleGeminiProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<GoogleGeminiProviderOptions>, GoogleGeminiProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton<IGoogleGeminiContentTranslator, GoogleGeminiContentTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IGoogleGeminiResponseParser, GoogleGeminiResponseParser>();
            services.TryAddSingleton<IGoogleGeminiEmbeddingRequestTranslator, GoogleGeminiEmbeddingRequestTranslator>();
            services.TryAddSingleton<IGoogleGeminiEmbeddingResponseParser, GoogleGeminiEmbeddingResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

            return services;
        }

        /// <summary>
        /// Registers a static Gemini API key as the credential source for
        /// every Gemini chat model, sent as the <c>x-goog-api-key</c>
        /// header.
        /// </summary>
        /// <param name="apiKey">The non-empty Gemini API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Gemini provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Gemini credential source an application (or
        /// <c>AddGoogleGeminiOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddGoogleGeminiApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId, credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Gemini chat model, sent as a standard
        /// <c>Authorization: Bearer</c> header (for a Google Cloud identity
        /// with Generative Language API scope).
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
        /// This registration is singular for the Gemini provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Gemini credential source an application (or
        /// <c>AddGoogleGeminiApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddGoogleGeminiOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(GoogleGeminiProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                GoogleGeminiProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named Gemini chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Gemini's own model identifier, such as <c>"gemini-2.5-flash"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="GoogleGeminiProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="GoogleGeminiProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddGoogleGemini"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddGoogleGeminiLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    GoogleGeminiProviderDefaults.ProviderId,
                    GoogleGeminiProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? GoogleGeminiProviderDefaults.DefaultCapabilities,
                    limits ?? GoogleGeminiProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new GoogleGeminiLlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IGoogleGeminiContentTranslator>(),
                    provider.GetRequiredService<IGoogleGeminiResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one named Gemini embedding model as an additional
        /// <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Gemini's own embedding model identifier, such as <c>"text-embedding-004"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input/output limits, or <see langword="null"/> to use
        /// <see cref="GoogleGeminiProviderDefaults.DefaultEmbeddingLimits"/>.
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
        /// <see cref="AddGoogleGemini"/> must be called first.
        /// </remarks>
        public IServiceCollection AddGoogleGeminiEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<GoogleGeminiProviderOptions>>().Value;

                var descriptor = new EmbeddingModelDescriptor(
                    alias,
                    GoogleGeminiProviderDefaults.ProviderId,
                    GoogleGeminiProviderDefaults.EmbeddingApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? GoogleGeminiProviderDefaults.DefaultEmbeddingCapabilities,
                    limits ?? GoogleGeminiProviderDefaults.DefaultEmbeddingLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new GoogleGeminiEmbeddingModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IGoogleGeminiEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<IGoogleGeminiEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleGeminiProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
