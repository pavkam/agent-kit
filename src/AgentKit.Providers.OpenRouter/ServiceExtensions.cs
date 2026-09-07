// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

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
/// <c>AddOpenRouterChatModel</c> is called once per named model an
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
        /// response parser, default <see cref="TimeProvider"/>, and a
        /// dedicated <see cref="HttpClient"/>.
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
        /// <c>AddOpenRouterChatModel</c>.
        /// </remarks>
        public IServiceCollection AddOpenRouter(Action<OpenRouterProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<OpenRouterProviderOptions>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<OpenRouterProviderOptions>, OpenRouterProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

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
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddOpenRouterOAuthCredential</c>) already registered.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddOpenRouterApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddSingleton<IProviderCredentialSource>(credentialSource);

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
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddOpenRouterApiKeyCredential</c>) already registered.
        /// </remarks>
        public IServiceCollection AddOpenRouterOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IOAuthAccessTokenProvider, TProvider>();
            services.TryAddSingleton<IProviderCredentialSource, DelegatingOAuthCredentialSource>();

            return services;
        }

        /// <summary>
        /// Registers one named, OpenRouter-routed chat model as an
        /// additional <see cref="IChatModel"/> implementation.
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
        /// <c>IEnumerable&lt;IChatModel&gt;</c>. <see cref="AddOpenRouter"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddOpenRouterChatModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IChatModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<OpenRouterProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    OpenRouterProviderDefaults.ProviderId,
                    OpenRouterProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? OpenRouterProviderDefaults.DefaultCapabilities,
                    limits ?? OpenRouterProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new OpenRouterChatModel(
                    descriptor,
                    OpenRouterProviderDefaults.CreateProfile(options),
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
