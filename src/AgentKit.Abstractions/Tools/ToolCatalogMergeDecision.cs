// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed outcome of catalog collision policy.</summary>
/// <remarks>Only a complete bounded selection or rejection is supported; the catalog revalidates selections before constructing its snapshot.</remarks>
public abstract record ToolCatalogMergeDecision
{
    /// <summary>Restricts decision cases to the contract assembly.</summary>
    /// <remarks>External policies instantiate the defined selection and rejection cases.</remarks>
    private protected ToolCatalogMergeDecision() { }
}
