// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One authorized request body supplied as memory or a staged stream.</summary>
public interface INetworkRequestContent
{
    /// <summary>Gets the media type of the request body.</summary>
    public string ContentType { get; }

    /// <summary>Gets the secret-free body fingerprint used for authorization.</summary>
    public ContentHash BodyFingerprint { get; }
}
