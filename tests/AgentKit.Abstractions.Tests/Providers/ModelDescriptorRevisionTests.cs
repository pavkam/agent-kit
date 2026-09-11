// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelDescriptorRevision behavior and contracts.</summary>
public sealed class ModelDescriptorRevisionTests: Conformance.LongIdentityConformanceTests<ModelDescriptorRevision>
{
    [Fact]
    public void ModelDescriptorRevision_WhenMaximumValueProvided_PreservesValue()
    {
        var revision = new ModelDescriptorRevision(long.MaxValue);
        revision.Value.ShouldBe(long.MaxValue);
        revision.ToString().ShouldBe(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    protected override ModelDescriptorRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ModelDescriptorRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
