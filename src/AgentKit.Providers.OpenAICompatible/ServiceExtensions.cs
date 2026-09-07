// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the shared OpenAI-compatible
/// protocol-family building blocks.
/// </summary>
/// <remarks>
/// This package is a toolkit for concrete provider integrations, not a
/// provider identity by itself. It registers only the default request
/// translator and response parser; a concrete package such as
/// AgentKit.Providers.OpenAI additionally registers its own model
/// descriptor, compatibility profile, credential source, and chat model.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the default <see cref="IOpenAIRequestTranslator"/>,
        /// <see cref="IOpenAIStreamParser"/>,
        /// <see cref="IIdentifierGenerator{ToolCallId}"/>,
        /// <see cref="IOpenAIEmbeddingRequestTranslator"/>, and
        /// <see cref="IOpenAIEmbeddingResponseParser"/> implementations,
        /// unless a registration for any of them already exists.
        /// </summary>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is idempotent and additive-safe: it uses
        /// <c>TryAdd</c> semantics, so calling it more than once, or after a
        /// concrete package or application has already replaced one of
        /// these defaults, never overwrites an existing registration.
        /// </remarks>
        public IServiceCollection AddOpenAICompatibleProvider()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IOpenAIRequestTranslator, OpenAIRequestTranslator>();
            services.TryAddSingleton<IOpenAIStreamParser, OpenAIChatCompletionResponseParser>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IOpenAIEmbeddingRequestTranslator, OpenAIEmbeddingRequestTranslator>();
            services.TryAddSingleton<IOpenAIEmbeddingResponseParser, OpenAIEmbeddingResponseParser>();

            return services;
        }
    }
}
