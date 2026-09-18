// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the DeepSeek conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddDeepSeek"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddDeepSeekApiKeyCredential</c> or
/// <c>AddDeepSeekOAuthCredential</c> configures authentication, and
/// <c>AddDeepSeekLlmModel</c> is called once per named model an
/// application wants to use. No default fabricates an API key, endpoint, or
/// model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the DeepSeek endpoint and wire-behavior options, along
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
        /// <c>AddDeepSeekApiKeyCredential</c>,
        /// <c>AddDeepSeekOAuthCredential</c>, and
        /// <c>AddDeepSeekLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddDeepSeek(Action<DeepSeekProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<DeepSeekProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<DeepSeekProviderOptions>, DeepSeekProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

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
        /// Registers a static DeepSeek API key as the credential source for
        /// every DeepSeek chat model.
        /// </summary>
        /// <param name="apiKey">The non-empty DeepSeek API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the DeepSeek provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// DeepSeek credential source an application (or
        /// <c>AddDeepSeekOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddDeepSeekApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                DeepSeekProviderDefaults.ProviderId,
                credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every DeepSeek chat model.
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
        /// This registration is singular for the DeepSeek provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// DeepSeek credential source an application (or
        /// <c>AddDeepSeekApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddDeepSeekOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(
                DeepSeekProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                DeepSeekProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named DeepSeek chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">DeepSeek's own model identifier, such as <c>"deepseek-chat"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="DeepSeekProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="DeepSeekProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddDeepSeek"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddDeepSeekLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddDeepSeekLlmModel(new ModelDescriptor(
                alias,
                DeepSeekProviderDefaults.ProviderId,
                DeepSeekProviderDefaults.ApiFamily,
                modelId,
                deploymentId: null,
                capabilities ?? DeepSeekProviderDefaults.DefaultCapabilities,
                limits ?? DeepSeekProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty));
        }

        /// <summary>
        /// Registers one DeepSeek conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">
        /// The exact descriptor the adapter will serve; it must name the DeepSeek provider and API family.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was registered with, so
        /// publish this same instance to the catalog (for example through <c>AddModelDescriptors</c>) rather than
        /// rebuilding an equivalent one. Additive; <see cref="AddDeepSeek"/> must be called first.
        /// </remarks>
        public IServiceCollection AddDeepSeekLlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != DeepSeekProviderDefaults.ProviderId || descriptor.ApiFamily != DeepSeekProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the DeepSeek provider and API family.", nameof(descriptor));
            }

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<DeepSeekProviderOptions>>().Value;

                return new DeepSeekLlmModel(
                    descriptor,
                    DeepSeekProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(DeepSeekProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }

        /// <summary>
        /// Registers one DeepSeek conversational model from the bundled <see cref="KnownModelCatalog"/>: both the
        /// <see cref="ILlmModel"/> adapter and the matching catalog descriptor, built from the model's published
        /// limits, capabilities, and list prices.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">The vendor's own model identifier; it must exist in the catalog under the DeepSeek provider.</param>
        /// <param name="catalog">The catalog to consult, or <see langword="null"/> for <see cref="KnownModelCatalog.Default"/>.</param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="alias"/> or <paramref name="modelId"/> is blank, or <paramref name="modelId"/> is not a known
        /// DeepSeek model.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is the one-call form of <c>AddDeepSeekLlmModel(ModelDescriptor)</c> followed by <c>AddModelDescriptors</c>
        /// with an identical descriptor. Both registrations are additive; the descriptor source is keyed
        /// <c>deepseek.known/{alias}</c>. <see cref="AddDeepSeek"/> must be called first and <c>AddAgentProviders</c>
        /// must be registered for the descriptor to be published.
        /// </para>
        /// <para>
        /// The catalog is reference data with stated provenance, not runtime discovery. A model the catalog does not
        /// know can still be registered explicitly with <c>AddDeepSeekLlmModel(alias, modelId, capabilities, limits)</c>
        /// followed by <c>AddModelDescriptors</c>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddDeepSeekKnownLlmModel(
            ModelAlias alias,
            ModelId modelId,
            KnownModelCatalog? catalog = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId.Value, nameof(modelId));

            catalog ??= KnownModelCatalog.Default;
            if (!catalog.TryFind(DeepSeekProviderDefaults.ProviderId, modelId, out var known))
            {
                throw new ArgumentException(
                    $"'{modelId.Value}' is not a DeepSeek model in the known-model catalog; register it explicitly with {nameof(AddDeepSeekLlmModel)}.",
                    nameof(modelId));
            }

            var descriptor = known.ToDescriptor(alias, DeepSeekProviderDefaults.ApiFamily, DeepSeekProviderDefaults.DefaultCapabilities);
            _ = services.AddDeepSeekLlmModel(descriptor);
            _ = services.AddModelDescriptors(new ModelDescriptorSourceId($"deepseek.known/{alias.Value}"), [descriptor]);
            return services;
        }
    }
}
