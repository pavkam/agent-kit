// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes how much of a <see cref="ReasoningContent"/> value is
/// observable outside the producing provider.
/// </summary>
/// <remarks>
/// Providers differ widely in how they expose model reasoning: some return
/// it in full, some charge for it but redact the text for safety or
/// competitive reasons, and some return only an opaque signature that a
/// follow-up request must echo back to reuse cached reasoning without the
/// text ever leaving the provider. Treating all three the same would either
/// leak a false sense of visibility or silently drop the information a
/// continuation request needs to stay compatible.
/// </remarks>
public enum ReasoningVisibility
{
    /// <summary>The reasoning text is fully visible in <see cref="ReasoningContent.Text"/>.</summary>
    Visible,

    /// <summary>
    /// The provider redacted the reasoning text; it may still be billed as
    /// usage even though it is not observable here.
    /// </summary>
    Redacted,

    /// <summary>
    /// The reasoning is represented only by an opaque, provider-verifiable
    /// signature token usable on a compatible continuation path, with no
    /// visible text at all.
    /// </summary>
    EncryptedSignature
}
