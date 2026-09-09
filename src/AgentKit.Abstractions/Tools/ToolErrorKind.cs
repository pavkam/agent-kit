// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the safe source of a terminal tool error.</summary>
/// <remarks>This closed classification is distinct from <see cref="ToolTerminalStatus"/> and never carries an exception type or provider status registry.</remarks>
public enum ToolErrorKind
{
    /// <summary>A tool declared a safe failure.</summary>
    Tool,
    /// <summary>A transport produced a safe failure.</summary>
    Transport,
    /// <summary>An effecting host produced a safe failure.</summary>
    Host,
    /// <summary>Policy processing produced a safe failure.</summary>
    Policy,
    /// <summary>Representation serialization produced a safe failure.</summary>
    Serialization,
    /// <summary>Protocol processing produced a safe failure.</summary>
    Protocol,
}
