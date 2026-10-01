// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

/// <summary>Creates distinct enforcement-intent identities for tests.</summary>
internal sealed class TestIntentIds: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
