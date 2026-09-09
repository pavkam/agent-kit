// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolCatalogConformanceFixture: Conformance.IToolCatalogConformanceFixture
{
    public IToolCatalog Create(IEnumerable<ITool> tools) => new ToolCatalog(tools);
}
