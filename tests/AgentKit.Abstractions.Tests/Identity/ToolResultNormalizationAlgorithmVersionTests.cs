// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ToolResultNormalizationAlgorithmVersionTests: Conformance.LongIdentityConformanceTests<ToolResultNormalizationAlgorithmVersion>
{

    /// <inheritdoc/>
    protected override ToolResultNormalizationAlgorithmVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ToolResultNormalizationAlgorithmVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
