// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

using AgentKit.Providers;
using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the OpenAI conversational provider
/// integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddOpenAI"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddOpenAIApiKeyCredential</c> or <c>AddOpenAIOAuthCredential</c>
/// configures authentication, and <c>AddOpenAILlmModel</c> is called once
/// per named model an application wants to use. No default fabricates an
/// API key, endpoint, or model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the OpenAI endpoint and wire-behavior options, along
        /// with the shared OpenAI-compatible request translator, response
        /// parser, default <see cref="TimeProvider"/>, and a dedicated
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
        /// <c>AddOpenAIApiKeyCredential</c>, <c>AddOpenAIOAuthCredential</c>,
        /// and <c>AddOpenAILlmModel</c>.
        /// </remarks>
        public IServiceCollection AddOpenAI(Action<OpenAIProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<OpenAIProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<OpenAIProviderOptions>, OpenAIProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton(TimeProvider.System);
            // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered
            // (non-streaming) attempt regardless of the caller's LlmModelRequest.Deadline, since
            // this adapter's own deadlineSource is layered on top of, not instead of, the
            // transport-level timeout. The per-request deadlineSource already bounds every attempt.
            services.TryAddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

            return services;
        }

        /// <summary>
        /// Registers a static OpenAI API key as the credential source for
        /// every OpenAI chat model.
        /// </summary>
        /// <param name="apiKey">The non-empty OpenAI API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the OpenAI provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides an OpenAI
        /// credential source an application (or
        /// <c>AddOpenAIOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddOpenAIApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                OpenAIProviderDefaults.ProviderId,
                credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every OpenAI chat model.
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
        /// This registration is singular for the OpenAI provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides an OpenAI
        /// credential source an application (or
        /// <c>AddOpenAIApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddOpenAIOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(
                OpenAIProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                OpenAIProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named OpenAI chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">OpenAI's own model identifier, such as <c>"gpt-4o"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="OpenAIProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="OpenAIProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddOpenAI"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddOpenAILlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    OpenAIProviderDefaults.ProviderId,
                    OpenAIProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? OpenAIProviderDefaults.DefaultCapabilities,
                    limits ?? OpenAIProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new OpenAILlmModel(
                    descriptor,
                    OpenAIProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one OpenAI conversational model from the bundled
        /// <see cref="KnownModelCatalog"/>: both the <see cref="ILlmModel"/> adapter
        /// and the matching catalog descriptor, built from the model's published
        /// limits, capabilities, and list prices.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">OpenAI's own model identifier, such as <c>"gpt-4o-mini"</c>; it must exist in <see cref="KnownModelCatalog.Default"/>.</param>
        /// <param name="catalog">The catalog to consult, or <see langword="null"/> for <see cref="KnownModelCatalog.Default"/>.</param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="modelId"/> is not a known OpenAI model.</exception>
        /// <remarks>
        /// <para>
        /// This is the one-call form of <c>AddOpenAILlmModel(ModelDescriptor)</c> followed by
        /// <c>AddModelDescriptors</c> with an identical descriptor. Both registrations
        /// are additive; the descriptor source is keyed <c>openai.known/{alias}</c>.
        /// <see cref="AddOpenAI"/> must be called first and <c>AddAgentProviders</c>
        /// must be registered for the descriptor to be published.
        /// </para>
        /// <para>
        /// The catalog is reference data with stated provenance, not runtime
        /// discovery: it records what the vendor published at import time. A model
        /// the catalog does not know can still be registered explicitly with
        /// <c>AddOpenAILlmModel(alias, modelId, capabilities, limits)</c>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddOpenAIKnownLlmModel(
            ModelAlias alias,
            ModelId modelId,
            KnownModelCatalog? catalog = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId.Value, nameof(modelId));

            catalog ??= KnownModelCatalog.Default;
            if (!catalog.TryFind(OpenAIProviderDefaults.ProviderId, modelId, out var known))
            {
                throw new ArgumentException(
                    $"'{modelId.Value}' is not an OpenAI model in the known-model catalog; register it explicitly with {nameof(AddOpenAILlmModel)}.",
                    nameof(modelId));
            }

            var descriptor = known.ToDescriptor(alias, OpenAIProviderDefaults.ApiFamily, OpenAIProviderDefaults.DefaultCapabilities);
            _ = services.AddOpenAILlmModel(descriptor);
            _ = services.AddModelDescriptors(new ModelDescriptorSourceId($"openai.known/{alias.Value}"), [descriptor]);
            return services;
        }

        /// <summary>
        /// Registers one OpenAI conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">The exact descriptor the adapter will serve; it must name the OpenAI provider and API family.</param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was
        /// registered with, so publish this same instance to the catalog (for example through
        /// <c>AddModelDescriptors</c>) rather than rebuilding an equivalent one. Additive; <see cref="AddOpenAI"/> must be called first.
        /// </remarks>
        public IServiceCollection AddOpenAILlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != OpenAIProviderDefaults.ProviderId || descriptor.ApiFamily != OpenAIProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the OpenAI provider and chat-completions API family.", nameof(descriptor));
            }

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value;
                return new OpenAILlmModel(
                    descriptor,
                    OpenAIProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one named OpenAI embedding model as an additional
        /// <see cref="IEmbeddingModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">OpenAI's own model identifier, such as <c>"text-embedding-3-small"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="OpenAIProviderDefaults.DefaultEmbeddingCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's input/output limits, or <see langword="null"/> to use
        /// <see cref="OpenAIProviderDefaults.DefaultEmbeddingLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;IEmbeddingModel&gt;</c>. <see cref="AddOpenAI"/>
        /// must be called first. Embedding models use the same credential
        /// registered through <c>AddOpenAIApiKeyCredential</c> or
        /// <c>AddOpenAIOAuthCredential</c> as conversational models; the two
        /// operation kinds remain independently selectable aliases over the
        /// same OpenAI account.
        /// </remarks>
        public IServiceCollection AddOpenAIEmbeddingModel(
            EmbeddingModelAlias alias,
            ModelId modelId,
            EmbeddingCapabilities? capabilities = null,
            EmbeddingLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IEmbeddingModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenAIProviderOptions>>().Value;

                var descriptor = new EmbeddingModelDescriptor(
                    alias,
                    OpenAIProviderDefaults.ProviderId,
                    OpenAIProviderDefaults.EmbeddingApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? OpenAIProviderDefaults.DefaultEmbeddingCapabilities,
                    limits ?? OpenAIProviderDefaults.DefaultEmbeddingLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new OpenAIEmbeddingModel(
                    descriptor,
                    OpenAIProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIEmbeddingRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIEmbeddingResponseParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(OpenAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
