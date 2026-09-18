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
            services.TryAddSingleton<ICohereEmbeddingRequestTranslator, CohereEmbeddingRequestTranslator>();
            services.TryAddSingleton<ICohereEmbeddingResponseParser, CohereEmbeddingResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            // PooledConnectionLifetime is bounded (not the SocketsHttpHandler default of infinite) so a
            // long-lived process singleton periodically re-resolves DNS and re-verifies the connection
            // instead of pinning to one address for the process lifetime - the well-documented
            // singleton-HttpClient pitfall. Registered as its own replaceable singleton so a caller can
            // override the transport policy (e.g. a custom DelegatingHandler chain) without also having
            // to replace the HttpClient registration below.
            services.TryAddSingleton(_ => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });
            // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered
            // (non-streaming) attempt regardless of the caller's LlmModelRequest.Deadline, since
            // this adapter's own deadlineSource is layered on top of, not instead of, the
            // transport-level timeout. The per-request deadlineSource already bounds every attempt.
            services.TryAddSingleton(provider => new HttpClient(provider.GetRequiredService<SocketsHttpHandler>())
            {
                Timeout = Timeout.InfiniteTimeSpan,
            });

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

            return services.AddCohereLlmModel(new ModelDescriptor(
                alias,
                CohereProviderDefaults.ProviderId,
                CohereProviderDefaults.ApiFamily,
                modelId,
                deploymentId: null,
                capabilities ?? CohereProviderDefaults.DefaultCapabilities,
                limits ?? CohereProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty));
        }

        /// <summary>
        /// Registers one Cohere conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">
        /// The exact descriptor the adapter will serve; it must name the Cohere provider and API family.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was registered with, so
        /// publish this same instance to the catalog (for example through <c>AddModelDescriptors</c>) rather than
        /// rebuilding an equivalent one. Additive; <see cref="AddCohere"/> must be called first.
        /// </remarks>
        public IServiceCollection AddCohereLlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != CohereProviderDefaults.ProviderId || descriptor.ApiFamily != CohereProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the Cohere provider and API family.", nameof(descriptor));
            }

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value;

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

        /// <summary>
        /// Registers one Cohere conversational model from the bundled <see cref="KnownModelCatalog"/>: both the
        /// <see cref="ILlmModel"/> adapter and the matching catalog descriptor, built from the model's published
        /// limits, capabilities, and list prices.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">The vendor's own model identifier; it must exist in the catalog under the Cohere provider.</param>
        /// <param name="catalog">The catalog to consult, or <see langword="null"/> for <see cref="KnownModelCatalog.Default"/>.</param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="alias"/> or <paramref name="modelId"/> is blank, or <paramref name="modelId"/> is not a known
        /// Cohere model.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is the one-call form of <c>AddCohereLlmModel(ModelDescriptor)</c> followed by <c>AddModelDescriptors</c>
        /// with an identical descriptor. Both registrations are additive; the descriptor source is keyed
        /// <c>cohere.known/{alias}</c>. <see cref="AddCohere"/> must be called first and <c>AddAgentProviders</c>
        /// must be registered for the descriptor to be published.
        /// </para>
        /// <para>
        /// The catalog is reference data with stated provenance, not runtime discovery. A model the catalog does not
        /// know can still be registered explicitly with <c>AddCohereLlmModel(alias, modelId, capabilities, limits)</c>
        /// followed by <c>AddModelDescriptors</c>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddCohereKnownLlmModel(
            ModelAlias alias,
            ModelId modelId,
            KnownModelCatalog? catalog = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId.Value, nameof(modelId));

            catalog ??= KnownModelCatalog.Default;
            if (!catalog.TryFind(CohereProviderDefaults.ProviderId, modelId, out var known))
            {
                throw new ArgumentException(
                    $"'{modelId.Value}' is not a Cohere model in the known-model catalog; register it explicitly with {nameof(AddCohereLlmModel)}.",
                    nameof(modelId));
            }

            var descriptor = known.ToDescriptor(alias, CohereProviderDefaults.ApiFamily, CohereProviderDefaults.DefaultCapabilities);
            _ = services.AddCohereLlmModel(descriptor);
            _ = services.AddModelDescriptors(new ModelDescriptorSourceId($"cohere.known/{alias.Value}"), [descriptor]);
            return services;
        }

        /// <summary>
        /// Registers one named Cohere embedding model as an additional
        /// <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Cohere's own model identifier, such as <c>"embed-v4.0"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="CohereProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input limits, or <see langword="null"/> to use
        /// <see cref="CohereProviderDefaults.DefaultEmbeddingLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;IEmbeddingModel&gt;</c>. <see cref="AddCohere"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddCohereEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CohereProviderOptions>>().Value;

                var descriptor = new EmbeddingModelDescriptor(
                    alias,
                    CohereProviderDefaults.ProviderId,
                    CohereProviderDefaults.EmbeddingApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? CohereProviderDefaults.DefaultEmbeddingCapabilities,
                    limits ?? CohereProviderDefaults.DefaultEmbeddingLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new CohereEmbeddingModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<ICohereEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<ICohereEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(CohereProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
