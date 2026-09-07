// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One reason a composition or agent definition was rejected.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The stable <see cref="Code"/> exists so a host can react programmatically
/// and so error text can change without breaking callers. The message is for
/// humans and must stay free of credentials, prompts, and model output.
/// </para>
/// </remarks>
public sealed record CompositionDiagnostic
{
    private readonly string _code;
    private readonly string _safeMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositionDiagnostic"/>
    /// record.
    /// </summary>
    /// <param name="code">
    /// A stable, machine-readable identifier for this class of problem.
    /// </param>
    /// <param name="safeMessage">
    /// A redacted, human-readable explanation of what is wrong and, where
    /// possible, what to change.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="safeMessage"/> is null,
    /// empty, or consists only of whitespace.
    /// </exception>
    public CompositionDiagnostic(string code, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        _code = code;
        _safeMessage = safeMessage;
    }

    /// <summary>Gets the stable, machine-readable problem identifier.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Code
    {
        get => _code;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Code));
            _code = value;
        }
    }

    /// <summary>Gets the redacted, human-readable explanation.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeMessage
    {
        get => _safeMessage;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeMessage));
            _safeMessage = value;
        }
    }

    /// <summary>
    /// Returns the diagnostic as <c>code: message</c>, suitable for logging
    /// and aggregated composition-failure text.
    /// </summary>
    public override string ToString() => $"{_code}: {_safeMessage}";
}
