// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The file was written successfully through legacy <see cref="IFileSystem"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
[Obsolete("Use FileWriteSuccess from the spec IFileWriter contract instead.")]
public sealed record LegacyFileWritten: LegacyFileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="LegacyFileWritten"/> record.</summary>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytesWritten"/> is negative.</exception>
    public LegacyFileWritten(long bytesWritten)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytesWritten);
        BytesWritten = bytesWritten;
    }

    /// <summary>Gets the number of bytes written.</summary>
    public long BytesWritten { get; init; }
}
