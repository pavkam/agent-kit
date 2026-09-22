// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

using AgentKit.Context;
using AgentKit.Tools;

/// <summary>Registers skill discovery and activation over one shared immutable catalog.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the skill tool and context inventory source from the same catalog instance.</summary>
        /// <param name="configure">Optional skill definitions and host ceilings.</param>
        /// <param name="assemblerKey">The assembler profile that receives the skill inventory contributor.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddSkillTool(
            Action<SkillToolOptions>? configure = null,
            ComponentKey<IContextAssembler>? assemblerKey = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            assemblerKey ??= AgentContextComponentDefaults.AssemblerKey;
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
            _ = services.AddToolInvoker<SkillTool>(SkillTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == SkillTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(SkillTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, SkillTool>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IToolPresentationFormatter, SkillToolPresentationFormatter>());
            _ = services.AddContextContributor<SkillInventoryContextContributor>(
                (ComponentKey<IContextAssembler>) assemblerKey,
                new ContextContributorRegistration(
                    new ContextSourceKey("agentkit.tools.skill.inventory"),
                    order: 60,
                    ContextEvaluationFrequency.OncePerRun,
                    required: false));
            return services;
        }
    }
}
