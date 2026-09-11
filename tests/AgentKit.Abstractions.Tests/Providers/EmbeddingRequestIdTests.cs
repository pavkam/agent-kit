// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingRequestId behavior and contracts.</summary>
public sealed class EmbeddingRequestIdTests: Conformance.GuidIdentityConformanceTests<EmbeddingRequestId>
{
    [Fact]
    public void EmbeddingRequestId_Constructor_WhenEmptyGuid_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingRequestId(Guid.Empty));
    [Fact]
    public void EmbeddingRequestId_ToString_ReturnsGuidDFormat()
    {
        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        new EmbeddingRequestId(guid).ToString().ShouldBe(guid.ToString("D"));
    }

    /// <inheritdoc/>
    protected override EmbeddingRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(EmbeddingRequestId subject) => subject.Value;
}
