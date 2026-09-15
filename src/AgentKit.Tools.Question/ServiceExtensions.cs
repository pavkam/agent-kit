// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question;

/// <summary>Registers the human-question tool as an independent coding-harness feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers the question tool, its identity source, and validated host ceilings.</summary>
        /// <param name="configure">Optional host ceiling configuration.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddQuestionTool(Action<QuestionToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<QuestionToolOptions>()
                .Validate(static value => value.DefaultTimeout > TimeSpan.Zero, "DefaultTimeout must be positive.")
                .Validate(static value => value.MaximumTimeout >= value.DefaultTimeout, "MaximumTimeout must not be less than DefaultTimeout.")
                .Validate(static value => value.MaximumPromptCharacters > 0, "MaximumPromptCharacters must be positive.")
                .Validate(static value => value.MaximumLabelCharacters > 0, "MaximumLabelCharacters must be positive.")
                .Validate(static value => value.MaximumDescriptionCharacters > 0, "MaximumDescriptionCharacters must be positive.")
                .Validate(static value => value.MaximumAnswerCharacters > 0, "MaximumAnswerCharacters must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<QuestionId>, GuidQuestionIdGenerator>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, QuestionTool>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolPresentationFormatter, QuestionToolPresentationFormatter>());
            return services;
        }
    }
}
