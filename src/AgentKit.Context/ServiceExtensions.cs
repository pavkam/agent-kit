// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the built-in, reduced-scope
/// context assembler.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in assembler under the default loop-aligned key.</summary>
        /// <param name="configure">Optional configuration for <see cref="AgentContextOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Equivalent to <c>AddAgentContext(AgentContextComponentDefaults.AssemblerKey, configure)</c>.
        /// Idempotent: repeated equivalent calls keep the first registration.
        /// </remarks>
        public IServiceCollection AddAgentContext(Action<AgentContextOptions>? configure = null) =>
            services.AddAgentContext(AgentContextComponentDefaults.AssemblerKey, configure);

        /// <summary>Registers the built-in <see cref="DefaultContextAssembler"/> and budgeting services under an explicit key.</summary>
        /// <param name="key">The stable key this assembler registration is selected by.</param>
        /// <param name="configure">Optional configuration for <see cref="AgentContextOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Uses <c>TryAddKeyedScoped</c> semantics for the assembler. When <paramref name="key"/> is the default
        /// loop-aligned key, an unkeyed <see cref="IContextAssembler"/> registration forwards to the keyed default.
        /// </remarks>
        public IServiceCollection AddAgentContext(
            ComponentKey<IContextAssembler> key,
            Action<AgentContextOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();
            return AgentContextRegistration.Add(services, key, configure);
        }

        /// <summary>Additively registers a custom keyed <see cref="IContextAssembler"/> implementation.</summary>
        /// <typeparam name="TAssembler">The scoped assembler implementation.</typeparam>
        /// <param name="key">The stable key this assembler registration is selected by.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddContextAssembler<TAssembler>(ComponentKey<IContextAssembler> key)
            where TAssembler : class, IContextAssembler =>
            AgentContextRegistration.AddAssembler<TAssembler>(services, key);

        /// <summary>Replaces whatever <see cref="IContextAssembler"/> is registered under a key.</summary>
        /// <typeparam name="TAssembler">The scoped replacement implementation.</typeparam>
        /// <param name="key">The stable key whose registration is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceContextAssembler<TAssembler>(ComponentKey<IContextAssembler> key)
            where TAssembler : class, IContextAssembler =>
            AgentContextRegistration.ReplaceAssembler<TAssembler>(services, key);

        /// <summary>Additively registers one context contributor for an assembler profile.</summary>
        /// <typeparam name="TContributor">The contributor implementation resolved from the current scope.</typeparam>
        /// <param name="assembler">The assembler profile that owns the contributor.</param>
        /// <param name="registration">The contributor's stable registration metadata.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddContextContributor<TContributor>(
            ComponentKey<IContextAssembler> assembler,
            ContextContributorRegistration registration)
            where TContributor : class, IContextContributor =>
            AgentContextRegistration.AddContributor<TContributor>(services, assembler, registration);

        /// <summary>Replaces the budget allocator selected by one assembler profile.</summary>
        /// <typeparam name="TAllocator">The allocator implementation.</typeparam>
        /// <param name="assembler">The assembler profile whose allocator is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceContextBudgetAllocator<TAllocator>(ComponentKey<IContextAssembler> assembler)
            where TAllocator : class, IContextBudgetAllocator =>
            AgentContextRegistration.ReplaceBudgetAllocator<TAllocator>(services, assembler);
    }
}
