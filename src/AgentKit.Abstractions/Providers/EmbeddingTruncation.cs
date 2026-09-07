// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable policy for handling an <see cref="EmbeddingInput"/> that
/// exceeds a model's maximum input length.
/// </summary>
public enum EmbeddingTruncation
{
    /// <summary>Use the provider's own default truncation behavior.</summary>
    ProviderDefault,

    /// <summary>Reject an overlength input as an error rather than truncating it.</summary>
    Reject,

    /// <summary>Truncate from the start of the input, keeping the end.</summary>
    Start,

    /// <summary>Truncate from the end of the input, keeping the start.</summary>
    End,
}
