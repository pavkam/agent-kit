// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Bridges the tool-catalog contract to the provider-neutral shape a model request advertises.</summary>
/// <remarks>
/// <see cref="ToolDescriptor"/> and <see cref="LlmToolDefinition"/> describe the same tool for two different
/// audiences: the catalog/authorization pipeline and a chat model request, respectively. Nothing else in
/// <c>AgentKit.Abstractions</c> converts between them, so every host composing an <see cref="AgentLoopRunRequest"/>
/// from a resolved <see cref="IToolCatalog"/> would otherwise hand-write this exact mapping.
/// </remarks>
public static class ToolDescriptorExtensions
{
    extension(ToolDescriptor descriptor)
    {
        /// <summary>Projects this descriptor to the minimal shape a model request advertises.</summary>
        /// <returns>
        /// A new <see cref="LlmToolDefinition"/> carrying this descriptor's identity, name, description, and
        /// input schema document.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The projection is lossy by design: <see cref="ToolDescriptor.Version"/>,
        /// <see cref="ToolDescriptor.OutputSchema"/>, <see cref="ToolDescriptor.Effects"/>,
        /// <see cref="ToolDescriptor.ExecutionHints"/>, <see cref="ToolDescriptor.SourceId"/>, and
        /// <see cref="ToolDescriptor.Extensions"/> have no counterpart in the model-facing shape and are not
        /// carried over. Catalog resolution, authorization, and execution must still use the original descriptor.
        /// </remarks>
        public LlmToolDefinition ToLlmToolDefinition()
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new LlmToolDefinition(
                descriptor.Id,
                descriptor.Name,
                descriptor.Description,
                descriptor.InputSchema.Document);
        }
    }
}
