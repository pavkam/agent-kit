// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Z.ai conversational provider
/// integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddZAi"/> configures the shared
/// endpoint and wire-behavior options, exactly one of
/// <c>AddZAiApiKeyCredential</c> or <c>AddZAiOAuthCredential</c> configures
/// authentication, and <c>AddZAiChatModel</c> is called once per named GLM
/// model an application wants to use. No default fabricates an API key,
/// endpoint, or model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Z.ai endpoint and wire-behavior options, along
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
        /// <c>AddZAiApiKeyCredential</c>, <c>AddZAiOAuthCredential</c>, and
        /// <c>AddZAiChatModel</c>.
        /// </remarks>
        public IServiceCollection AddZAi(Action<ZAiProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<ZAiProviderOptions>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<ZAiProviderOptions>, ZAiProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

            return services;
        }

        /// <summary>
        /// Registers a static Z.ai API key as the credential source for
        /// every Z.ai chat model.
        /// </summary>
        /// <param name="apiKey">The non-empty Z.ai API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddZAiOAuthCredential</c>) already registered. A static API
        /// key is Z.ai's documented primary credential; OAuth support is
        /// offered for parity with the other AgentKit provider
        /// integrations and for consoles that issue a derived token.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddZAiApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddSingleton<IProviderCredentialSource>(credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Z.ai chat model.
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
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddZAiApiKeyCredential</c>) already registered.
        /// </remarks>
        public IServiceCollection AddZAiOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IOAuthAccessTokenProvider, TProvider>();
            services.TryAddSingleton<IProviderCredentialSource, DelegatingOAuthCredentialSource>();

            return services;
        }

        /// <summary>
        /// Registers one named Z.ai chat model as an additional
        /// <see cref="IChatModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Z.ai's own model identifier, such as <c>"glm-4.6"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="ZAiProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="ZAiProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;IChatModel&gt;</c>. <see cref="AddZAi"/> must
        /// be called first.
        /// </remarks>
        public IServiceCollection AddZAiChatModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IChatModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ZAiProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    ZAiProviderDefaults.ProviderId,
                    ZAiProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? ZAiProviderDefaults.DefaultCapabilities,
                    limits ?? ZAiProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new ZAiChatModel(
                    descriptor,
                    ZAiProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredService<IProviderCredentialSource>(),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
