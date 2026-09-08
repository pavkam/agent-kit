// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Represents a typed result of capturing authorization for one exact operation scope.</summary>
public abstract record SecurityAuthorizationCaptureResult
{
    /// <summary>Initializes base state for a capture-result type.</summary>
    private protected SecurityAuthorizationCaptureResult()
    {
    }
}
