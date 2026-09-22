// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares whether automatic response decompression is permitted.</summary>
public enum NetworkDecompressionPolicy
{
    /// <summary>Do not automatically decompress response bodies.</summary>
    DenyAutomatic = 0,

    /// <summary>Allow the transport to decompress declared encodings within configured bounds.</summary>
    AllowAutomatic = 1,
}
