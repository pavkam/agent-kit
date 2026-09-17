// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Anthropic Claude conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddAnthropic"/> configures the
/// shared endpoint and wire-behavior options, exactly one of
/// <c>AddAnthropicApiKeyCredential</c> or
/// <c>AddAnthropicOAuthCredential</c> configures authentication, and
/// <c>AddAnthropicLlmModel</c> is called once per named model an
/// application wants to use. No default fabricates an API key, endpoint, or
/// model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Anthropic endpoint and wire-behavior options,
        /// along with the default request translator, response parser,
        /// default <see cref="TimeProvider"/>, and a dedicated
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
        /// <c>AddAnthropicApiKeyCredential</c>,
        /// <c>AddAnthropicOAuthCredential</c>, and
        /// <c>AddAnthropicLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddAnthropic(Action<AnthropicProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOptions<AnthropicProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AnthropicProviderOptions>, AnthropicProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            services.TryAddSingleton<IAnthropicMessageTranslator, AnthropicMessageTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IAnthropicMessageStreamParser, AnthropicMessageStreamParser>();
            services.TryAddSingleton(TimeProvider.System);
            // The BCL default HttpClient.Timeout (100s) would otherwise bound every buffered
            // (non-streaming) attempt regardless of the caller's LlmModelRequest.Deadline, since
            // this adapter's own deadlineSource is layered on top of, not instead of, the
            // transport-level timeout. The per-request deadlineSource already bounds every attempt.
            services.TryAddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

            return services;
        }

        /// <summary>
        /// Registers a static Anthropic API key as the credential source
        /// for every Anthropic chat model, sent as the <c>x-api-key</c>
        /// header.
        /// </summary>
        /// <param name="apiKey">The non-empty Anthropic API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Anthropic provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// Anthropic credential source an application (or
        /// <c>AddAnthropicOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddAnthropicApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(AnthropicProviderDefaults.ProviderId, credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Anthropic chat model, sent as a standard
        /// <c>Authorization: Bearer</c> header (for example, for Workload
        /// Identity Federation).
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
        /// This registration is singular for the Anthropic provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides an
        /// Anthropic credential source an application (or
        /// <c>AddAnthropicApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddAnthropicOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(AnthropicProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                AnthropicProviderDefaults.ProviderId,
                static (provider, key) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(key)));

            return services;
        }

        /// <summary>
        /// Registers one named Anthropic chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Anthropic's own model identifier, such as <c>"claude-sonnet-4-5"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="AnthropicProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="AnthropicProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddAnthropic"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddAnthropicLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AnthropicProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    AnthropicProviderDefaults.ProviderId,
                    AnthropicProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId: null,
                    capabilities ?? AnthropicProviderDefaults.DefaultCapabilities,
                    limits ?? AnthropicProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new AnthropicLlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IAnthropicMessageTranslator>(),
                    provider.GetRequiredService<IAnthropicMessageStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(AnthropicProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
