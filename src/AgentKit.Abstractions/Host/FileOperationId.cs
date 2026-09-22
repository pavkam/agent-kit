// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one file-system operation for correlation and grant binding.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. Callers create new operation identities through
/// <see cref="IIdentifierGenerator{T}"/> of <see cref="FileOperationId"/>.
/// </remarks>
public readonly record struct FileOperationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationId"/> struct,
    /// validating that it addresses a real operation rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real operation.
    /// </exception>
    public FileOperationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
