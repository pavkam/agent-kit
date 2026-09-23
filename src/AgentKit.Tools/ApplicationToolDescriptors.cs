// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Normalizes tool descriptors registered through the application tool source.</summary>
internal static class ApplicationToolDescriptors
{
    /// <summary>Rebinds one published descriptor to <see cref="ApplicationToolSources.Default"/> for discovery capture.</summary>
    /// <param name="descriptor">The complete tool descriptor supplied at registration.</param>
    /// <returns>An equivalent descriptor owned by the application source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    internal static ToolDescriptor ForApplicationSource(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.SourceId == ApplicationToolSources.Default
            ? descriptor
            : new ToolDescriptor(
            descriptor.Id,
            descriptor.Version,
            descriptor.Name,
            descriptor.Description,
            descriptor.InputSchema,
            descriptor.OutputSchema,
            descriptor.Effects,
            descriptor.ExecutionHints,
            ApplicationToolSources.Default,
            descriptor.Extensions);
    }
}

