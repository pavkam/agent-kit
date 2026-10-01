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
        /// Requires an <see cref="IFileSystemSelector"/> whose profile named by <see cref="ProjectInstructionOptions.ProfileKey"/>
        /// supports <see cref="FileSystemCapability.Read"/>, an <see cref="ISecurityAuthoritySelector"/>, and
        /// <c>AgentKit.Context</c> registration for the same assembler key. <see cref="ProjectInstructionOptions.HostRootPath"/>
        /// has no default and is validated when the host starts. Duplicate calls add another options configuration and another
        /// contributor registration for the key, as <c>AddContextContributor</c> documents.
        /// </remarks>
        public IServiceCollection AddProjectInstructionContributor(
            ComponentKey<IContextAssembler> assemblerKey,
            Action<ProjectInstructionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var optionsBuilder = services.AddOptions<ProjectInstructionOptions>()
                .Validate(static o => o.MaxBytesPerFile > 0, "MaxBytesPerFile must be positive.")
                .Validate(static o => !string.IsNullOrWhiteSpace(o.HostRootPath), "HostRootPath must be configured.")
                .Validate(static o => o.SearchRoots.Length > 0, "SearchRoots must not be empty.")
                .Validate(static o => o.InstructionFilenames.Length > 0, "InstructionFilenames must not be empty.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>>(
                static _ => new GuidSecurityRequestIdGenerator());
            services.TryAddSingleton<IIdentifierGenerator<FileOperationId>>(
                static _ => new GuidFileOperationIdGenerator());
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
