// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Generic dependency-injection helpers for registering application- or
/// package-supplied implementations of narrow AgentKit.Abstractions
/// contracts, such as <see cref="IIdentifierGenerator{TIdentifier}"/>.
/// </summary>
/// <remarks>
/// AgentKit.Abstractions intentionally contains no concrete service
/// defaults — no default identifier generator, no default clock, no default
/// anything that performs work. Registering an actual implementation is the
/// job of whichever feature package or application owns that default (for
/// example, the downstream AgentKit facade package registers the
/// cryptographically strong default <see cref="IIdentifierGenerator{TIdentifier}"/>
/// implementations). This class exists only so that registering a
/// <em>custom</em> implementation against one of these contracts follows the
/// same ordinary ASP.NET-style <see cref="IServiceCollection"/> pattern used
/// everywhere else in AgentKit, rather than requiring callers to hand-write
/// the keyed/typed registration boilerplate themselves.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds one explicit component-registration declaration for later
        /// composition validation.
        /// </summary>
        /// <param name="registration">
        /// The immutable contract, key, implementation, lifetime, and direct
        /// dependency declaration that must correspond to an ordinary
        /// Microsoft DI registration in the same collection.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Declarations are additive and this method intentionally retains
        /// duplicates so composition validation can reject ambiguous or
        /// repeated evidence. It does not register the described component,
        /// build a provider, resolve a service, or invoke a factory.
        /// </para>
        /// <para>
        /// A component registered through an opaque implementation factory
        /// must still declare its concrete implementation type and complete
        /// direct dependency graph here. Validation never executes a factory
        /// to infer either fact.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> or <paramref name="registration"/> is
        /// <see langword="null"/>.
        /// </exception>
        public IServiceCollection DeclareAgentKitComponent(ComponentRegistrationDescriptor registration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registration);

            _ = services.AddSingleton(registration);
            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TGenerator"/> as the singleton
        /// <see cref="IIdentifierGenerator{TIdentifier}"/> used to create
        /// every new <typeparamref name="TIdentifier"/> value, unless a
        /// generator for that closed identity type is already registered.
        /// </summary>
        /// <typeparam name="TIdentifier">
        /// The closed identity value type this generator produces, such as
        /// <see cref="RunId"/> or <see cref="MessageId"/>.
        /// </typeparam>
        /// <typeparam name="TGenerator">
        /// The generator implementation to register. It must be safe to call
        /// from multiple threads concurrently for the lifetime of the
        /// singleton registration.
        /// </typeparam>
        /// <returns>
        /// The same <paramref name="services"/> instance, so calls can be
        /// chained with other registration methods.
        /// </returns>
        /// <remarks>
        /// This registration is singular per closed <typeparamref name="TIdentifier"/>
        /// type and uses <c>TryAdd</c> semantics: whichever application or
        /// feature package registers a generator for that identity type
        /// first wins, and later calls with the same
        /// <typeparamref name="TIdentifier"/> are no-ops rather than
        /// silently replacing the earlier registration. This method never
        /// builds or resolves a service provider; the generator is only
        /// constructed later, when something else in the container first
        /// requests it.
        /// </remarks>
        public IServiceCollection TryAddIdentifierGenerator<TIdentifier, TGenerator>()
            where TIdentifier : struct
            where TGenerator : class, IIdentifierGenerator<TIdentifier>
        {
            services.TryAddSingleton<IIdentifierGenerator<TIdentifier>, TGenerator>();
            return services;
        }
    }
}
