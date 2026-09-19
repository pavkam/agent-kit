// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Represents the closed terminal outcome of one coordinated discovery, merge, and schema/capability preflight attempt.</summary>
/// <remarks>Only a complete capture, a merge rejection, or a schema/capability rejection is supported. Every case already released source ownership except the successful capture, which transfers it to the returned catalog.</remarks>
internal abstract record ToolCatalogCoordinatorResult
{
    /// <summary>Restricts outcome cases to this package.</summary>
    private protected ToolCatalogCoordinatorResult() { }
}
