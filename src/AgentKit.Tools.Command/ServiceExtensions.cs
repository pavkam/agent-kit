// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command;

/// <summary>Registers the explicit shell-command tool feature.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Additively registers one command tool and validates its model-facing profile.</summary>
        /// <param name="configure">Optional shell identity and bound configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddCommandTool(Action<CommandToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<CommandToolOptions>()
                .Validate(
                    static value => !string.IsNullOrWhiteSpace(value.ShellExecutable)
                        && !value.ShellExecutable.Contains('\0', StringComparison.Ordinal),
                    "ShellExecutable is required and cannot contain NUL.")
                .Validate(
                    static value => value.ShellArguments.All(
                        static argument => argument is not null && !argument.Contains('\0', StringComparison.Ordinal)),
                    "ShellArguments cannot contain null or NUL.")
                .Validate(static value => !string.IsNullOrWhiteSpace(value.SandboxProfile.Value), "SandboxProfile is required.")
                .Validate(
                    static value => value.DefaultTimeout > TimeSpan.Zero && value.DefaultTimeout <= value.MaximumTimeout,
                    "Timeout bounds are invalid.")
                .Validate(
                    static value => value.DefaultMaximumOutputBytes > 0
                        && value.DefaultMaximumOutputBytes <= value.MaximumOutputBytes,
                    "Output bounds are invalid.")
                .Validate(static value => value.TerminationGracePeriod >= TimeSpan.Zero, "Termination grace must be non-negative.")
                .Validate(static value => value.MaximumCommandBytes > 0, "MaximumCommandBytes must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, CommandTool>());
            return services;
        }
    }
}
