// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI;

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
/// deliberately separate calls: <see cref="AddZAI"/> configures the shared
/// endpoint and wire-behavior options, exactly one of
/// <c>AddZAIApiKeyCredential</c> or <c>AddZAIOAuthCredential</c> configures
/// authentication, and <c>AddZAILlmModel</c> is called once per named GLM
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
        /// <c>AddZAIApiKeyCredential</c>, <c>AddZAIOAuthCredential</c>, and
        /// <c>AddZAILlmModel</c>.
        /// </remarks>
        public IServiceCollection AddZAI(Action<ZAIProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<ZAIProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<ZAIProviderOptions>, ZAIProviderOptionsValidator>());

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
        /// This registration is singular for the Z.ai provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides a Z.ai
        /// credential source an application (or
        /// <c>AddZAIOAuthCredential</c>) already registered, while remaining
        /// isolated from other provider packages. A static API key is Z.ai's
        /// documented primary credential; OAuth support is offered for parity
        /// with the other AgentKit provider integrations and for consoles that
        /// issue a derived token.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddZAIApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                ZAIProviderDefaults.ProviderId,
                credentialSource);

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
        /// This registration is singular for the Z.ai provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides a Z.ai
        /// credential source an application (or
        /// <c>AddZAIApiKeyCredential</c>) already registered, while remaining
        /// isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddZAIOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(
                ZAIProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                ZAIProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named Z.ai chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Z.ai's own model identifier, such as <c>"glm-4.6"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="ZAIProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="ZAIProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddZAI"/> must
        /// be called first.
        /// </remarks>
        public IServiceCollection AddZAILlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ZAIProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    ZAIProviderDefaults.ProviderId,
                    ZAIProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? ZAIProviderDefaults.DefaultCapabilities,
                    limits ?? ZAIProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new ZAILlmModel(
                    descriptor,
                    ZAIProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(ZAIProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
