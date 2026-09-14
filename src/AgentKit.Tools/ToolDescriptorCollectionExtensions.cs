// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Bulk-projects a sequence of tool descriptors to the shape a model request advertises.</summary>
/// <remarks>
/// Kept as its own type rather than merged into <see cref="ToolDescriptorExtensions"/>: two <c>extension</c>
/// blocks over <see cref="ToolDescriptor"/> and <see cref="IEnumerable{ToolDescriptor}"/> in one class trip
/// CA1708, because the compiler-synthesized containers for both blocks share a name that the analyzer
/// compares case-insensitively regardless of their distinct receiver types.
/// </remarks>
public static class ToolDescriptorCollectionExtensions
{
    extension(IEnumerable<ToolDescriptor> descriptors)
    {
        /// <summary>Projects every descriptor in this sequence to the shape a model request advertises.</summary>
        /// <returns>
        /// An immutable array with one <see cref="LlmToolDefinition"/> per input descriptor, in enumeration order.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="descriptors"/> is <see langword="null"/>.</exception>
        public ImmutableArray<LlmToolDefinition> ToLlmToolDefinitions()
        {
            ArgumentNullException.ThrowIfNull(descriptors);
            return [.. descriptors.Select(static descriptor => descriptor.ToLlmToolDefinition())];
        }
    }
}
