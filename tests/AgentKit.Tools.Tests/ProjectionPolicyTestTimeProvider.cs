// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

internal sealed class ProjectionPolicyTestTimeProvider(Func<long> timestamp): TimeProvider
{
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => timestamp();
}
