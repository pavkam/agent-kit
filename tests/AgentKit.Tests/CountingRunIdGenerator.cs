// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Counts run identity allocation while producing valid identities for facade tests.</summary>
internal sealed class CountingRunIdGenerator: IIdentifierGenerator<RunId>
{
    /// <summary>Gets how many identities have been allocated.</summary>
    public int Created { get; private set; }

    /// <inheritdoc/>
    public RunId Create()
    {
        Created++;
        return new RunId(Guid.NewGuid());
    }
}
