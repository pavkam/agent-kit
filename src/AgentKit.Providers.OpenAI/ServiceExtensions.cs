// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

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
/// configures authentication, and <c>AddOpenAIChatModel</c> is called once
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
        /// and <c>AddOpenAIChatModel</c>.
        /// </remarks>
        public IServiceCollection AddOpenAI(Action<OpenAIProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<OpenAIProviderOptions>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<OpenAIProviderOptions>, OpenAIProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

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
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddOpenAIOAuthCredential</c>) already registered.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddOpenAIApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new OpenAIApiKeyCredentialSource(apiKey);
            services.TryAddSingleton<IProviderCredentialSource>(credentialSource);

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
        /// This registration is singular: it uses <c>TryAdd</c> semantics,
        /// so it never overrides a credential source an application (or
        /// <c>AddOpenAIApiKeyCredential</c>) already registered.
        /// </remarks>
        public IServiceCollection AddOpenAIOAuthCredential<TProvider>()
            where TProvider : class, IOpenAIOAuthTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IOpenAIOAuthTokenProvider, TProvider>();
            services.TryAddSingleton<IProviderCredentialSource, OpenAIOAuthTokenCredentialSource>();

            return services;
        }

        /// <summary>
        /// Registers one named OpenAI chat model as an additional
        /// <see cref="IChatModel"/> implementation.
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
        /// <c>IEnumerable&lt;IChatModel&gt;</c>. <see cref="AddOpenAI"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddOpenAIChatModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<IChatModel>(provider =>
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

                return new OpenAIChatModel(
                    descriptor,
                    OpenAIProviderDefaults.CreateProfile(options),
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
