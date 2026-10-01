// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one durable journal write.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="DurableRecorded"/>, <see cref="DurableRecordFenced"/>, and
/// <see cref="DurableRecordFailed"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind and defeat exhaustive
/// handling by recovery code.
/// </remarks>
public abstract record DurableRecordResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableRecordResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    private protected DurableRecordResult()
    {
    }

    /// <summary>
    /// Fails closed unless this write is durably recorded, so a boundary never proceeds to an effect that recovery
    /// could not later account for.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The write was refused because this attempt lost ownership (<see cref="DurableRecordFenced"/>), or could not
    /// be committed or authorized (<see cref="DurableRecordFailed"/>). The message is content-free and includes the
    /// journal's own safe failure text when one exists.
    /// </exception>
    /// <remarks>
    /// Call this before an effect whose evidence is what makes a later recovery honest, such as a provider request
    /// or a tool invocation. It is unnecessary after an effect that has already happened, where the write is
    /// confirming evidence and a refusal must not undo work that already took place.
    /// </remarks>
    public void ThrowIfNotRecorded()
    {
        switch (this)
        {
            case DurableRecorded:
                return;
            case DurableRecordFenced:
                throw new InvalidOperationException(
                    "The durable write was refused because this attempt no longer owns the operation.");
            case DurableRecordFailed failed:
                throw new InvalidOperationException(
                    $"The durable write could not be recorded: {failed.SafeMessage}");
            default:
                throw new InvalidOperationException("The durable write was not recorded.");
        }
    }
}
