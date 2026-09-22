// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcomes for <see cref="IFileReader.OpenReadAsync"/>.</summary>
public abstract record FileReadOpenResult
{
    /// <summary>Initializes one closed open outcome.</summary>
    private protected FileReadOpenResult()
    {
    }
}
