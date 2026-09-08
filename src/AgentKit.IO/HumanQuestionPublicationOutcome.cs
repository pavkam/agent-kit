// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Classifies the bounded terminal state of one human-question publication attempt.</summary>
internal enum HumanQuestionPublicationOutcome
{
    /// <summary>The application channel returned an authenticated answer.</summary>
    Answered,
    /// <summary>The channel reported that the response deadline elapsed.</summary>
    TimedOut,
    /// <summary>The channel could not present or resolve the question.</summary>
    ChannelUnavailable,
    /// <summary>The grant store denied or could not prove fresh receipt evidence before channel dispatch.</summary>
    GrantDenied,
    /// <summary>Captured authorization did not describe the concrete publication request.</summary>
    CapturedAuthorizationMismatch,
    /// <summary>The caller cancelled its wait.</summary>
    Cancelled,
    /// <summary>An unexpected dependency failure interrupted the operation.</summary>
    Failed,
}
