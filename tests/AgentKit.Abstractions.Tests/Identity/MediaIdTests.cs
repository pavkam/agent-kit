// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class MediaIdTests: Conformance.GuidIdentityConformanceTests<MediaId>
{

    /// <inheritdoc/>
    protected override MediaId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(MediaId subject) => subject.Value;
}
