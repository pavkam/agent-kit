// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares when the host closes standard input for one process operation.</summary>
public enum ProcessStandardInputDelivery
{
    /// <summary>Writes the initial payload, then closes standard input before waiting for exit.</summary>
    AfterInitialPayload = 0,

    /// <summary>Keeps standard input open until <see cref="IProcessHandle.CompleteStandardInputAsync"/>.</summary>
    Streaming = 1,
}
