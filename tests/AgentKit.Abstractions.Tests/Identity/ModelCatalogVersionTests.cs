// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ModelCatalogVersionTests: Conformance.LongIdentityConformanceTests<ModelCatalogVersion>
{

    /// <inheritdoc/>
    protected override ModelCatalogVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ModelCatalogVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
