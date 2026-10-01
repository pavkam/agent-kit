// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Groq;

using AgentKit.Providers;
using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Groq conversational
/// provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddGroq"/> configures the shared
/// endpoint and wire-behavior options, exactly one of
/// <c>AddGroqApiKeyCredential</c> or <c>AddGroqOAuthCredential</c> configures
/// authentication, and <c>AddGroqLlmModel</c> is called once per named
/// model an application wants to use. No default fabricates an API key,
/// endpoint, or model an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Groq endpoint and wire-behavior options,
        /// along with the shared OpenAI-compatible request translator,
        /// response parser, default <see cref="TimeProvider"/>. Adapters built from
        /// these registrations send through <see cref="ProviderEgress"/> and own no HTTP client.
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
        /// <c>AddGroqApiKeyCredential</c>, <c>AddGroqOAuthCredential</c>,
        /// and <c>AddGroqLlmModel</c>.
        /// </remarks>
        public IServiceCollection AddGroq(Action<GroqProviderOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddOpenAICompatibleProvider();
            _ = services.AddOptions<GroqProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<GroqProviderOptions>, GroqProviderOptionsValidator>());

            if (configureOptions is not null)
            {
                _ = services.Configure(configureOptions);
            }

            _ = OpenAICompatibleProviderProfileRegistration.RegisterChatProfiles(
                services,
                GroqProviderDefaults.ProviderId,
                GroqProviderDefaults.ChatServiceSurface,
                GroqProviderDefaults.DefaultBaseAddress,
                GroqProviderDefaults.CredentialSourceKey,
                GroqProviderDefaults.ChatEndpointProfileKey,
                GroqProviderDefaults.ChatCredentialProfileKey,
                GroqProviderDefaults.DefaultEndpointId);

            services.TryAddSingleton(TimeProvider.System);

            return services;
        }

        /// <summary>
        /// Registers a static Groq API key as the credential
        /// source for every Groq chat model.
        /// </summary>
        /// <param name="apiKey">The non-empty Groq API key.</param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Groq provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides a Groq
        /// credential source an application (or
        /// <c>AddGroqOAuthCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="apiKey"/> is null, empty, or consists only of
        /// whitespace.
        /// </exception>
        public IServiceCollection AddGroqApiKeyCredential(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credentialSource = new StaticApiKeyCredentialSource(apiKey);
            _ = OpenAICompatibleProviderProfileRegistration.RegisterDualKeyCredentialSource(
                services,
                GroqProviderDefaults.CredentialSourceKey,
                GroqProviderDefaults.ProviderId,
                credentialSource);

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TProvider"/> as the OAuth access
        /// token source for every Groq chat model.
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
        /// This registration is singular for the Groq provider key: it uses
        /// keyed <c>TryAdd</c> semantics, so it never overrides a Groq
        /// credential source an application (or
        /// <c>AddGroqApiKeyCredential</c>) already registered, while
        /// remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddGroqOAuthCredential<TProvider>()
            where TProvider : class, IOAuthAccessTokenProvider
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(
                GroqProviderDefaults.ProviderId);
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                GroqProviderDefaults.CredentialSourceKey,
                static (provider, _) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(GroqProviderDefaults.ProviderId)));
            services.TryAddKeyedSingleton<IProviderCredentialSource>(
                GroqProviderDefaults.ProviderId,
                static (provider, _) => new DelegatingOAuthCredentialSource(
                    provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(GroqProviderDefaults.ProviderId)));

            return services;
        }

        /// <summary>
        /// Registers one named Groq chat model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">Groq's own model identifier, such as <c>"llama-3.3-70b-versatile"</c>.</param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="GroqProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="GroqProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddGroq"/> must
        /// be called first.
        /// </remarks>
        public IServiceCollection AddGroqLlmModel(
            ModelAlias alias,
            ModelId modelId,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddGroqLlmModel(new ModelDescriptor(
                alias,
                GroqProviderDefaults.ProviderId,
                GroqProviderDefaults.ApiFamily,
                modelId,
                deploymentId: null,
                capabilities ?? GroqProviderDefaults.DefaultCapabilities,
                limits ?? GroqProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty));
        }

        /// <summary>
        /// Registers one Groq conversational model from a complete descriptor as an
        /// additional <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="descriptor">
        /// The exact descriptor the adapter will serve; it must name the Groq provider and API family.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> names another provider or API family.</exception>
        /// <remarks>
        /// The adapter rejects a request whose selected descriptor differs from the one it was registered with, so
        /// publish this same instance to the catalog (for example through <c>AddModelDescriptors</c>) rather than
        /// rebuilding an equivalent one. Additive; <see cref="AddGroq"/> must be called first.
        /// </remarks>
        public IServiceCollection AddGroqLlmModel(ModelDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.ProviderId != GroqProviderDefaults.ProviderId || descriptor.ApiFamily != GroqProviderDefaults.ApiFamily)
            {
                throw new ArgumentException("The descriptor must name the Groq provider and API family.", nameof(descriptor));
            }

            var boundDescriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                descriptor,
                GroqProviderDefaults.ChatServiceSurface,
                GroqProviderDefaults.ChatEndpointProfileKey,
                GroqProviderDefaults.ChatCredentialProfileKey,
                GroqProviderDefaults.DefaultEndpointId);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<GroqProviderOptions>>().Value;

                return new GroqLlmModel(
                    boundDescriptor,
                    GroqProviderDefaults.CreateProfile(options),
                    provider.GetRequiredService<IOpenAIRequestTranslator>(),
                    provider.GetRequiredService<IOpenAIStreamParser>(),
                    provider.GetRequiredKeyedService<IProviderCredentialSource>(GroqProviderDefaults.ProviderId),
                    provider.GetRequiredService<ProviderEgress>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetService<IProviderProfileRuntimeSelector>());
            });

            return services;
        }

        /// <summary>
        /// Registers one Groq conversational model from the bundled <see cref="KnownModelCatalog"/>: both the
        /// <see cref="ILlmModel"/> adapter and the matching catalog descriptor, built from the model's published
        /// limits, capabilities, and list prices.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">The vendor's own model identifier; it must exist in the catalog under the Groq provider.</param>
        /// <param name="catalog">The catalog to consult, or <see langword="null"/> for <see cref="KnownModelCatalog.Default"/>.</param>
        /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="alias"/> or <paramref name="modelId"/> is blank, or <paramref name="modelId"/> is not a known
        /// Groq model.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is the one-call form of <c>AddGroqLlmModel(ModelDescriptor)</c> followed by <c>AddModelDescriptors</c>
        /// with an identical descriptor. Both registrations are additive; the descriptor source is keyed
        /// <c>groq.known/{alias}</c>. <see cref="AddGroq"/> must be called first and <c>AddAgentProviders</c>
        /// must be registered for the descriptor to be published.
        /// </para>
        /// <para>
        /// The catalog is reference data with stated provenance, not runtime discovery. A model the catalog does not
        /// know can still be registered explicitly with <c>AddGroqLlmModel(alias, modelId, capabilities, limits)</c>
        /// followed by <c>AddModelDescriptors</c>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddGroqKnownLlmModel(
            ModelAlias alias,
            ModelId modelId,
            KnownModelCatalog? catalog = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId.Value, nameof(modelId));

            catalog ??= KnownModelCatalog.Default;
            if (!catalog.TryFind(GroqProviderDefaults.ProviderId, modelId, out var known))
            {
                throw new ArgumentException(
                    $"'{modelId.Value}' is not a Groq model in the known-model catalog; register it explicitly with {nameof(AddGroqLlmModel)}.",
                    nameof(modelId));
            }

            var descriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                known.ToDescriptor(alias, GroqProviderDefaults.ApiFamily, GroqProviderDefaults.DefaultCapabilities),
                GroqProviderDefaults.ChatServiceSurface,
                GroqProviderDefaults.ChatEndpointProfileKey,
                GroqProviderDefaults.ChatCredentialProfileKey,
                GroqProviderDefaults.DefaultEndpointId);
            _ = services.AddGroqLlmModel(descriptor);
            _ = services.AddModelDescriptors(new ModelDescriptorSourceId($"groq.known/{alias.Value}"), [descriptor]);
            return services;
        }
    }
}
