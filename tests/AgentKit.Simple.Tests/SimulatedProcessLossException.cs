// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Signals that a test killed the simulated process before a durable record could be committed.</summary>
internal sealed class SimulatedProcessLossException: Exception
{
    /// <summary>Initializes the exception with its fixed, content-free message.</summary>
    public SimulatedProcessLossException()
        : base("The simulated process was lost before the terminal record was committed.")
    {
    }
}
