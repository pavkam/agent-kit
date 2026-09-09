// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes one catalog from caller-supplied borrowed tool registrations.</summary>
public interface IToolCatalogConformanceFixture
{
    /// <summary>Creates an immutable catalog without taking ownership of the supplied tools.</summary>
    /// <param name="tools">The tool instances whose descriptors the catalog must capture exactly once.</param>
    /// <returns>The composed catalog under test.</returns>
    public IToolCatalog Create(IEnumerable<ITool> tools);
}
