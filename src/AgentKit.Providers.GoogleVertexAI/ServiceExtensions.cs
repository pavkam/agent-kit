// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Google Vertex AI
/// conversational provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddGoogleVertexAI"/> configures
/// the project, region, and wire-behavior options,
/// <see cref="AddGoogleVertexAIOAuthCredential{TProvider}"/> configures
/// authentication, and <c>AddGoogleVertexAILlmModel</c> is called
/// once per model an application wants to use. No default fabricates a
/// project, region, or credential an account may not actually have. Unlike
/// every other AgentKit provider package, there is no
/// <c>AddGoogleVertexAIApiKeyCredential</c> method: Vertex AI has no
/// API-key authentication mode, so offering one would misrepresent a
/// capability the platform does not have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Vertex AI project/region options, along with the
        /// shared Gemini request translator, response parser, default
        /// <see cref="TimeProvider"/>, and a dedicated
        /// <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="configureOptions">
        /// Configures the options, most importantly
        /// <see cref="GoogleVertexAIProviderOptions.ProjectId"/> and
        /// <see cref="GoogleVertexAIProviderOptions.Location"/>, which have
        /// no default and must be set here. Set
        /// <see cref="GoogleVertexAIProviderOptions.BaseAddress"/> only to
        /// target a host other than the one Google documents for the
        /// location (regional, or <c>https://aiplatform.googleapis.com/</c>
        /// for <c>global</c>).
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This method must be called exactly once; calling it more than
        /// once applies <paramref name="configureOptions"/> more than once
        /// through the ordinary <c>Microsoft.Extensions.Options</c>
        /// configuration pipeline. The authentication and model
        /// registrations are independent calls documented on
        /// <see cref="AddGoogleVertexAIOAuthCredential{TProvider}"/> and
        /// <c>AddGoogleVertexAILlmModel</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="configureOptions"/> is null.</exception>
        public IServiceCollection AddGoogleVertexAI(Action<GoogleVertexAIProviderOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            _ = services.AddOptions<GoogleVertexAIProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<GoogleVertexAIProviderOptions>, GoogleVertexAIProviderOptionsValidator>());
            _ = services.Configure(configureOptions);

            services.TryAddSingleton<IGoogleGeminiContentTranslator, GoogleGeminiContentTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IGoogleGeminiResponseParser, GoogleGeminiResponseParser>();
            services.TryAddSingleton<IGoogleVertexAIEmbeddingRequestTranslator, GoogleVertexAIEmbeddingRequestTranslator>();
            services.TryAddSingleton<IGoogleVertexAIEmbeddingResponseParser, GoogleVertexAIEmbeddingResponseParser>();
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
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Vertex AI chat model, sent as an
        /// <c>Authorization: Bearer</c> header.
        /// </summary>
        /// <typeparam name="TProvider">
        /// The application-owned implementation that supplies and refreshes
        /// the current OAuth access token, typically sourced from Google
        /// Cloud Application Default Credentials with the relevant
        /// <c>aiplatform.*</c> IAM permissions.
        /// </typeparam>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Vertex AI provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Vertex AI credential source an application already registered,
        /// while remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddGoogleVertexAIOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(GoogleVertexAIProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                GoogleVertexAIProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one Vertex AI model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Gemini's own model identifier, such as <c>"gemini-2.5-flash"</c>.</param>
        /// <param name="deploymentId">
        /// When set, the ID of a deployed/custom Vertex AI endpoint to call
        /// instead of the shared publisher-model resource.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="GoogleVertexAIProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="GoogleVertexAIProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddGoogleVertexAI"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddGoogleVertexAILlmModel(
            ModelAlias alias,
            ModelId modelId,
            DeploymentId? deploymentId = null,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddGoogleVertexAILlmModel(new ModelDescriptor(
                alias,
                GoogleVertexAIProviderDefaults.ProviderId,
                GoogleVertexAIProviderDefaults.ApiFamily,
                modelId,
                deploymentId,
                capabilities ?? GoogleVertexAIProviderDefaults.DefaultCapabilities,
                limits ?? GoogleVertexAIProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty));
        }

        /// <summary>
        /// Registers one Google Vertex AI conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">
        /// The exact descriptor the adapter will serve; it must name the Google Vertex AI provider and API family.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was registered with, so
        /// publish this same instance to the catalog (for example through <c>AddModelDescriptors</c>) rather than
        /// rebuilding an equivalent one. Additive; <see cref="AddGoogleVertexAI"/> must be called first.
        /// </remarks>
        public IServiceCollection AddGoogleVertexAILlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != GoogleVertexAIProviderDefaults.ProviderId || descriptor.ApiFamily != GoogleVertexAIProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the Google Vertex AI provider and API family.", nameof(descriptor));
            }

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

                return new GoogleVertexAILlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IGoogleGeminiContentTranslator>(),
                    provider.GetRequiredService<IGoogleGeminiResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleVertexAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one Vertex AI text-embedding model as an additional
        /// <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">
        /// Vertex's own embedding model identifier, such as
        /// <c>"text-embedding-005"</c> or <c>"gemini-embedding-001"</c>.
        /// </param>
        /// <param name="deploymentId">
        /// When set, the ID of a deployed/custom Vertex AI endpoint to call
        /// instead of the shared publisher-model resource.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input/output limits, or <see langword="null"/> to use
        /// <see cref="GoogleVertexAIProviderDefaults.DefaultEmbeddingLimits"/>.
        /// A caller registering <c>gemini-embedding-001</c> should supply
        /// <c>maxInputsPerRequest: 1</c>, since that model accepts only a
        /// single input per request unlike Vertex's other embedding
        /// models.
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
        /// <see cref="AddGoogleVertexAI"/> must be called first.
        /// </remarks>
        public IServiceCollection AddGoogleVertexAIEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            DeploymentId? deploymentId = null,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<GoogleVertexAIProviderOptions>>().Value;

                var descriptor = new EmbeddingModelDescriptor(
                    alias,
                    GoogleVertexAIProviderDefaults.ProviderId,
                    GoogleVertexAIProviderDefaults.EmbeddingApiFamily,
                    modelId,
                    deploymentId,
                    capabilities ?? GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities,
                    limits ?? GoogleVertexAIProviderDefaults.DefaultEmbeddingLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new GoogleVertexAIEmbeddingModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IGoogleVertexAIEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<IGoogleVertexAIEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(GoogleVertexAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
