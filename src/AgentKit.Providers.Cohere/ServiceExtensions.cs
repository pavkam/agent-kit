// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Cohere conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddCohere"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddCohereApiKeyCredential</c> or <c>AddCohereOAuthCredential</c>
/// configures authentication, and <c>AddCohereLlmModel</c> is called once
/// per named model an application wants to use. No default fabricates an
/// API key, endpoint, or model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Cohere endpoint and wire-behavior options, along
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
        /// <c>AddCohereApiKeyCredential</c>, <c>AddCohereOAuthCredential</c>,
        /// and <c>AddCohereLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddCohere(Action<CohereProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOptions<CohereProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<CohereProviderOptions>, CohereProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton<ICohereRequestTranslator, CohereRequestTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<ICohereResponseParser, CohereResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

            return services;
        }

        /// <summary>
        /// Registers a static Cohere API key as the credential source for
        /// every Cohere chat model, sent as an <c>Authorization: Bearer</c>
        /// header.
        /// </summary>
        /// <param name="apiKey">The non-empty Cohere API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Cohere provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Cohere credential source an application (or
        /// <c>AddCohereOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddCohereApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(CohereProviderDefaults.ProviderId, credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Cohere chat model, sent through the same
        /// <c>Authorization: Bearer</c> header shape as a static API key.
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
        /// This registration is singular for the Cohere provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Cohere credential source an application (or
        /// <c>AddCohereApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddCohereOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(CohereProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                CohereProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named Cohere chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Cohere's own model identifier, such as <c>"command-a-plus-05-2026"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="CohereProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="CohereProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddCohere"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddCohereLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    CohereProviderDefaults.ProviderId,
                    CohereProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? CohereProviderDefaults.DefaultCapabilities,
                    limits ?? CohereProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new CohereLlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<ICohereRequestTranslator>(),
                    provider.GetRequiredService<ICohereResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
