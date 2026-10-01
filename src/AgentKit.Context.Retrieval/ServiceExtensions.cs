// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval;

/// <summary>Registers the retrieval context contributor with an assembler profile.</summary>
/// <remarks>The registration does not install the retrieval pipeline or any memory profile; applications compose those through the memory package and select the profile on each agent definition.</remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers <see cref="RetrievalContextContributor"/> as a contributor of the named context assembler.</summary>
        /// <param name="assemblerKey">The assembler profile that owns the contributor.</param>
        /// <param name="configure">An optional callback that adjusts <see cref="RetrievalContextOptions"/>; successive calls compose.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">The service collection is null.</exception>
        /// <remarks>
        /// Options are validated when the host starts. Registering the same assembler twice follows the context package's duplicate
        /// contributor rules. The contributor evaluates once per model request so retrieval follows the newest user message.
        /// </remarks>
        public IServiceCollection AddRetrievalContextContributor(
            ComponentKey<IContextAssembler> assemblerKey,
            Action<RetrievalContextOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var optionsBuilder = services.AddOptions<RetrievalContextOptions>()
                .Validate(static options => Enum.IsDefined(options.MaximumClassification), "MaximumClassification must be a defined value.")
                .Validate(static options => options.MaximumItems > 0, "MaximumItems must be positive.")
                .Validate(static options => options.MaximumBytes > 0, "MaximumBytes must be positive.")
                .Validate(static options => options.MaximumTokens > 0, "MaximumTokens must be positive.")
                .Validate(static options => options.MaximumQueryCharacters > 0, "MaximumQueryCharacters must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            return services.AddContextContributor<RetrievalContextContributor>(
                assemblerKey,
                new ContextContributorRegistration(
                    new ContextSourceKey("agentkit.context.retrieval"),
                    order: 40,
                    ContextEvaluationFrequency.OncePerModelRequest,
                    required: false));
        }
    }
}
