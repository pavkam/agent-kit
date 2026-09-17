// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Registers trusted identity normalization, validation, and delegation services.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the first-party identity runtime with no implicit issuer or anonymous principal.</summary>
        /// <param name="configure">Optional binding configuration captured in an immutable validated snapshot.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Registration is idempotent. Issuers and normalization policies remain additive.</remarks>
        public IServiceCollection AddAgentIdentity(Action<AgentIdentityOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            var options = services.AddOptions<AgentIdentityOptions>()
                .Validate(static value => value.MaximumDelegationDepth > 0, "MaximumDelegationDepth must be positive.")
                .Validate(static value => value.MaximumClockSkew >= TimeSpan.Zero, "MaximumClockSkew must not be negative.")
                .Validate(static value => value.MaximumEvidenceLifetime > TimeSpan.Zero, "MaximumEvidenceLifetime must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(static provider =>
            {
                var value = provider.GetRequiredService<IOptions<AgentIdentityOptions>>().Value;
                return new AgentIdentityOptionsSnapshot(value.AllowAnonymous, value.MaximumDelegationDepth, value.MaximumClockSkew, value.MaximumEvidenceLifetime);
            });
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentityIssuerCatalog, IdentityIssuerCatalog>();
            services.TryAddSingleton<OrderedIdentityNormalizationPolicies>();
            services.TryAddSingleton<IIdentityValidationPolicy, DefaultIdentityValidationPolicy>();
            services.TryAddScoped<IExecutionIdentityResolver, ExecutionIdentityResolver>();
            services.TryAddSingleton<IDelegatedIdentityDeriver, DefaultDelegatedIdentityDeriver>();
            return services;
        }

        /// <summary>Additively registers one singleton trusted issuer under a stable key.</summary>
        /// <typeparam name="TIssuer">The issuer implementation.</typeparam>
        /// <param name="registration">The key used to select the issuer.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The issuer key is already registered.</exception>
        public IServiceCollection AddIdentityIssuer<TIssuer>(IdentityIssuerRegistration registration)
            where TIssuer : class, IIdentityIssuer
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registration);
            if (services.Any(service => service.ServiceType == typeof(IdentityIssuerRegistration) && service.ImplementationInstance is IdentityIssuerRegistration existing && existing.IssuerId == registration.IssuerId))
            {
                throw new InvalidOperationException("An identity issuer is already registered for this issuer key.");
            }

            _ = services.AddKeyedSingleton<TIssuer>(registration.IssuerId);
            _ = services.AddSingleton(registration);
            _ = services.AddSingleton(provider => new IdentityIssuerBinding(provider.GetRequiredKeyedService<TIssuer>(registration.IssuerId), registration));
            return services;
        }

        /// <summary>Additively registers one singleton normalization policy in a deterministic order.</summary>
        /// <typeparam name="TPolicy">The policy implementation.</typeparam>
        /// <param name="registration">The stable name and ordering metadata.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The stable policy name is already registered.</exception>
        public IServiceCollection AddIdentityNormalizationPolicy<TPolicy>(IdentityNormalizationPolicyRegistration registration)
            where TPolicy : class, IIdentityNormalizationPolicy
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registration);
            if (services.OfType<ServiceDescriptor>().Any(service => service.ServiceType == typeof(IdentityNormalizationPolicyRegistration) && service.ImplementationInstance is IdentityNormalizationPolicyRegistration existing && StringComparer.Ordinal.Equals(existing.Name, registration.Name)))
            {
                throw new InvalidOperationException("An identity normalization policy is already registered with this name.");
            }

            services.TryAddSingleton<TPolicy>();
            _ = services.AddSingleton(registration);
            _ = services.AddSingleton(provider => new IdentityNormalizationPolicyBinding(provider.GetRequiredService<TPolicy>(), registration));
            return services;
        }

        /// <summary>Replaces the singular identity-validation policy.</summary>
        /// <typeparam name="TPolicy">The replacement singleton policy.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceIdentityValidationPolicy<TPolicy>() where TPolicy : class, IIdentityValidationPolicy
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.RemoveAll<IIdentityValidationPolicy>();
            _ = services.AddSingleton<IIdentityValidationPolicy, TPolicy>();
            return services;
        }

        /// <summary>Replaces the singular delegated-identity deriver.</summary>
        /// <typeparam name="TDeriver">The replacement singleton deriver.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegatedIdentityDeriver<TDeriver>() where TDeriver : class, IDelegatedIdentityDeriver
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.RemoveAll<IDelegatedIdentityDeriver>();
            _ = services.AddSingleton<IDelegatedIdentityDeriver, TDeriver>();
            return services;
        }

        /// <summary>Replaces the scoped assertion resolver.</summary>
        /// <typeparam name="TResolver">The replacement scoped resolver.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceIdentityResolver<TResolver>() where TResolver : class, IExecutionIdentityResolver
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.RemoveAll<IExecutionIdentityResolver>();
            _ = services.AddScoped<IExecutionIdentityResolver, TResolver>();
            return services;
        }
    }
}
