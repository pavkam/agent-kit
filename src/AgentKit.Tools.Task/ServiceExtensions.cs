// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task;

using AgentKit.Tools;

/// <summary>Registers task delegation as an independent coding-harness feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the task tool, delegation identity source, and validated model-facing ceilings.</summary>
        /// <param name="configure">Optional ceiling configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddTaskTool(Action<TaskToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<TaskToolOptions>()
                .Validate(static value => value.DefaultTimeout > TimeSpan.Zero, "DefaultTimeout must be positive.")
                .Validate(static value => value.MaximumTimeout >= value.DefaultTimeout, "MaximumTimeout must not be less than DefaultTimeout.")
                .Validate(static value => value.DefaultMaximumTurns > 0 && value.MaximumTurns >= value.DefaultMaximumTurns, "Turn bounds are invalid.")
                .Validate(static value => value.DefaultMaximumToolCalls > 0 && value.MaximumToolCalls >= value.DefaultMaximumToolCalls, "Tool-call bounds are invalid.")
                .Validate(static value => value.MaximumObjectiveCharacters > 0 && value.MaximumAcceptanceCriteria > 0 && value.MaximumCriterionCharacters > 0 && value.MaximumAllowedTools > 0 && value.MaximumSummaryCharacters > 0, "Text and collection bounds must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<DelegationId>, GuidDelegationIdGenerator>();
            _ = services.AddToolInvoker<TaskTool>(TaskTool.Descriptor);
            if (!services.Any(static descriptor =>
                    descriptor.IsKeyedService
                    && descriptor.ServiceType == typeof(ToolsetPublication)
                    && descriptor.ServiceKey is ToolsetKey key
                    && key == TaskTool.DefaultToolset.Key))
            {
                _ = services.AddToolset(TaskTool.DefaultToolset);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, TaskTool>());
            return services;
        }
    }
}
