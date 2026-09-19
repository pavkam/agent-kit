// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Reports that one merged descriptor's input or output schema failed bounded canonical compilation preflight.</summary>
/// <remarks>The coordinator already released every discovered source before returning this outcome. No partial catalog is ever advertised.</remarks>
internal sealed record ToolCatalogCoordinatorSchemaRejected: ToolCatalogCoordinatorResult
{
    /// <summary>Retains the exact rejected descriptor, which of its schemas failed, and the classified reason.</summary>
    /// <param name="tool">The nonnull merged descriptor whose schema failed preflight.</param>
    /// <param name="isOutputSchema">True when <see cref="ToolDescriptor.OutputSchema"/> failed; false when <see cref="ToolDescriptor.InputSchema"/> failed.</param>
    /// <param name="rejection">The nonnull classified compilation rejection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tool"/> or <paramref name="rejection"/> is null.</exception>
    internal ToolCatalogCoordinatorSchemaRejected(ToolDescriptor tool, bool isOutputSchema, ToolSchemaCompilationRejected rejection)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(rejection);
        Tool = tool;
        IsOutputSchema = isOutputSchema;
        Rejection = rejection;
    }

    /// <summary>Gets the merged descriptor whose declared schema failed preflight.</summary>
    /// <value>A nonnull descriptor drawn from the already merged snapshot.</value>
    internal ToolDescriptor Tool { get; }

    /// <summary>Gets which declared schema failed preflight.</summary>
    /// <value>True for <see cref="ToolDescriptor.OutputSchema"/>; false for <see cref="ToolDescriptor.InputSchema"/>.</value>
    internal bool IsOutputSchema { get; }

    /// <summary>Gets the classified compilation rejection.</summary>
    /// <value>A nonnull rejection carrying no partial validation handle.</value>
    internal ToolSchemaCompilationRejected Rejection { get; }
}
