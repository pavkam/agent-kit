// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Represents a typed result of reading an exact published security profile.</summary>
/// <remarks>A missing result does not authorize fallback profile selection.</remarks>
public abstract record SecurityProfilePublicationResult
{
    /// <summary>Initializes base state for a publication-result type.</summary>
    private protected SecurityProfilePublicationResult()
    {
    }
}
