// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for workspace project instruction discovery.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers <see cref="ProjectInstructionContributor"/> for one assembler profile.</summary>
        /// <param name="assemblerKey">The assembler key that should evaluate the contributor.</param>
        /// <param name="configure">Optional discovery bounds.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Requires <see cref="IFileSystem"/>, <see cref="ISecurityAuthority"/>, and
        /// <c>AgentKit.Context</c> registration for the same assembler key.
        /// </remarks>
        public IServiceCollection AddProjectInstructionContributor(
            ComponentKey<IContextAssembler> assemblerKey,
            Action<ProjectInstructionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var optionsBuilder = services.AddOptions<ProjectInstructionOptions>()
                .Validate(static o => o.MaxBytesPerFile > 0, "MaxBytesPerFile must be positive.")
                .Validate(static o => o.SearchRoots.Length > 0, "SearchRoots must not be empty.")
                .Validate(static o => o.InstructionFilenames.Length > 0, "InstructionFilenames must not be empty.");
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>>(
                static _ => new GuidSecurityRequestIdGenerator());
            return services.AddContextContributor<ProjectInstructionContributor>(
                assemblerKey,
                new ContextContributorRegistration(
                    new ContextSourceKey("agentkit.context.project.instructions"),
                    order: 50,
                    ContextEvaluationFrequency.OncePerRun,
                    required: false));
        }
    }
}
