// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>
/// Thrown when an AWS event stream message is truncated, malformed, or
/// fails a checksum, so a corrupted <c>ConverseStream</c> frame is never
/// silently misinterpreted as valid content.
/// </summary>
internal sealed class AwsEventStreamFormatException: Exception
{
    /// <summary>Initializes a new instance of the <see cref="AwsEventStreamFormatException"/> class.</summary>
    /// <param name="message">A message describing the specific framing or checksum failure.</param>
    public AwsEventStreamFormatException(string message)
        : base(message)
    {
    }
}
