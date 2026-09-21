// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextManifestDisposition"/> values.</summary>
public sealed class ContextManifestDispositionTests
{
    [Fact]
    public void ContextManifestDisposition_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<ContextManifestDisposition>().ShouldBe(
        [
            ContextManifestDisposition.Included,
            ContextManifestDisposition.Transformed,
            ContextManifestDisposition.Omitted,
        ]);
}
