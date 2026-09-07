// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Registers skill discovery and activation over one shared immutable catalog.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the skill tool and context inventory source from the same catalog instance.</summary>
        /// <param name="configure">Optional skill definitions and host ceilings.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddSkillTool(Action<SkillToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<SkillToolOptions>()
                .Validate(static value => value.MaximumBytes > 0 && value.MaximumCharacters > 0 && value.MaximumNameCharacters > 0 && value.MaximumDescriptionCharacters > 0, "Skill bounds must be positive.")
                .Validate(static value => value.Skills.Select(static skill => skill.Id).Distinct().Count() == value.Skills.Count, "Skill IDs must be unique.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<ConfiguredSkillCatalog>();
            services.TryAddSingleton<ISkillCatalog>(static provider => provider.GetRequiredService<ConfiguredSkillCatalog>());
            services.TryAddSingleton<ISkillCatalogContextSource>(static provider => provider.GetRequiredService<ConfiguredSkillCatalog>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, SkillTool>());
            return services;
        }
    }
}
