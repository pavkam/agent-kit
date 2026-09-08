// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Registers session-backed plan state and its coding-harness tool.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the plan store, tool, generators, and validated host ceilings.</summary>
        /// <param name="configure">Optional model-facing bound configuration.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddPlanTool(Action<PlanToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<PlanToolOptions>()
                .Validate(static value => value.MaximumTitleCharacters > 0, "MaximumTitleCharacters must be positive.")
                .Validate(static value => value.MaximumItemCharacters > 0, "MaximumItemCharacters must be positive.")
                .Validate(static value => value.MaximumItemIdCharacters > 0, "MaximumItemIdCharacters must be positive.")
                .Validate(static value => value.MaximumItems is >= 1 and <= 50, "MaximumItems must be between one and fifty.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<PlanId>, GuidPlanIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>, GuidSessionEntryIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>,
                GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<IPlanStateStore, SessionPlanStateStore>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, PlanTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, TodoTool>());
            return services;
        }
    }
}
