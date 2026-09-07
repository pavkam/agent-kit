// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the Amazon Bedrock Runtime
/// conversational provider integration.
/// </summary>
/// <remarks>
/// Endpoint configuration, authentication, and model registration are three
/// deliberately separate calls: <see cref="AddAwsBedrock"/> configures the
/// region and wire-behavior options, <see cref="AddAwsBedrockStaticCredential"/>
/// or a custom <see cref="IAwsCredentialSource"/> registration configures
/// authentication, and <see cref="AddAwsBedrockLlmModel"/> is called once
/// per model an application wants to use. No default fabricates a region
/// or credential an account may not actually have.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Bedrock region/wire-behavior options, along with
        /// the request translator, response parser, default
        /// <see cref="TimeProvider"/>, and a dedicated
        /// <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="configureOptions">
        /// Configures the options, most importantly
        /// <see cref="AwsBedrockProviderOptions.Region"/>, which has no
        /// default and must be set here.
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
        /// <see cref="AddAwsBedrockStaticCredential"/> and
        /// <see cref="AddAwsBedrockLlmModel"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="configureOptions"/> is null.</exception>
        public IServiceCollection AddAwsBedrock(Action<AwsBedrockProviderOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            _ = services.AddOptions<AwsBedrockProviderOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AwsBedrockProviderOptions>, AwsBedrockProviderOptionsValidator>());
            _ = services.Configure(configureOptions);

            services.TryAddSingleton<IAwsBedrockRequestTranslator, AwsBedrockRequestTranslator>();
            services.TryAddSingleton<IIdentifierGenerator<ToolCallId>, DefaultToolCallIdGenerator>();
            services.TryAddSingleton<IAwsBedrockResponseParser, AwsBedrockResponseParser>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(_ => new HttpClient());

            return services;
        }

        /// <summary>
        /// Registers one fixed, caller-supplied AWS access key/secret
        /// key/session token as the credential every Bedrock request is
        /// signed with.
        /// </summary>
        /// <param name="accessKeyId">The AWS access key ID.</param>
        /// <param name="secretAccessKey">The AWS secret access key.</param>
        /// <param name="sessionToken">
        /// The AWS session token, when the credential is a temporary
        /// security credential, or <see langword="null"/> for a long-term
        /// IAM user credential.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Bedrock provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Bedrock credential source an application already registered,
        /// while remaining isolated from other provider packages. An
        /// application whose credentials are temporary (an assumed IAM
        /// role, AWS STS, or an EC2/ECS/Lambda instance credential
        /// provider) should register its own refreshing
        /// <see cref="IAwsCredentialSource"/> under the
        /// <see cref="AwsBedrockProviderDefaults.ProviderId"/> key instead
        /// of this method, since a static credential never refreshes or
        /// expires.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="accessKeyId"/> or <paramref name="secretAccessKey"/>
        /// is null, empty, or consists only of whitespace.
        /// </exception>
        public IServiceCollection AddAwsBedrockStaticCredential(
            string accessKeyId,
            string secretAccessKey,
            string? sessionToken = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var credential = new AwsSigV4Credential(accessKeyId, secretAccessKey, sessionToken);
            services.TryAddKeyedSingleton<IAwsCredentialSource>(
                AwsBedrockProviderDefaults.ProviderId,
                (_, _) => new StaticAwsCredentialSource(credential));

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TSource"/> as the AWS credential
        /// source for every Bedrock request.
        /// </summary>
        /// <typeparam name="TSource">
        /// The application-owned implementation that resolves and, when
        /// needed, refreshes the current <see cref="AwsSigV4Credential"/>.
        /// </typeparam>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular for the Bedrock provider key: it
        /// uses keyed <c>TryAdd</c> semantics, so it never overrides a
        /// Bedrock credential source an application already registered,
        /// while remaining isolated from other provider packages.
        /// </remarks>
        public IServiceCollection AddAwsBedrockCredentialSource<TSource>()
            where TSource : class, IAwsCredentialSource
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddKeyedSingleton<IAwsCredentialSource, TSource>(AwsBedrockProviderDefaults.ProviderId);

            return services;
        }

        /// <summary>
        /// Registers one Bedrock model as an additional
        /// <see cref="ILlmModel"/> implementation.
        /// </summary>
        /// <param name="alias">The application-facing selection key for this model.</param>
        /// <param name="modelId">
        /// The Bedrock base model ID, such as
        /// <c>anthropic.claude-3-sonnet-20240229-v1:0</c>.
        /// </param>
        /// <param name="deploymentId">
        /// When set, an ARN (a cross-region inference profile, provisioned
        /// throughput, custom model deployment, or Bedrock Marketplace
        /// endpoint) to invoke instead of <paramref name="modelId"/>
        /// directly.
        /// </param>
        /// <param name="capabilities">
        /// The model's capabilities, or <see langword="null"/> to use
        /// <see cref="AwsBedrockProviderDefaults.DefaultCapabilities"/>.
        /// </param>
        /// <param name="limits">
        /// The model's token limits, or <see langword="null"/> to use
        /// <see cref="AwsBedrockProviderDefaults.DefaultLimits"/>.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is additive: calling it more than once with a
        /// distinct <paramref name="alias"/> registers additional models
        /// alongside one another, resolvable together as
        /// <c>IEnumerable&lt;ILlmModel&gt;</c>. <see cref="AddAwsBedrock"/>
        /// must be called first.
        /// </remarks>
        public IServiceCollection AddAwsBedrockLlmModel(
            ModelAlias alias,
            ModelId modelId,
            DeploymentId? deploymentId = null,
            ModelCapabilities? capabilities = null,
            ModelLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILlmModel>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AwsBedrockProviderOptions>>().Value;

                var descriptor = new ModelDescriptor(
                    alias,
                    AwsBedrockProviderDefaults.ProviderId,
                    AwsBedrockProviderDefaults.ApiFamily,
                    modelId,
                    deploymentId,
                    capabilities ?? AwsBedrockProviderDefaults.DefaultCapabilities,
                    limits ?? AwsBedrockProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty);

                return new AwsBedrockLlmModel(
                    descriptor,
                    options,
                    provider.GetRequiredService<IAwsBedrockRequestTranslator>(),
                    provider.GetRequiredService<IAwsBedrockResponseParser>(),
                    provider.GetRequiredKeyedService<IAwsCredentialSource>(AwsBedrockProviderDefaults.ProviderId),
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<TimeProvider>());
            });

            return services;
        }
    }
}
