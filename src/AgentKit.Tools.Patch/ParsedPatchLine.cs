// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Represents one prefixed logical hunk line and its terminal-newline marker.</summary>
/// <param name="Prefix">The space, removal, or addition prefix.</param>
/// <param name="Text">The line content without prefix or terminator.</param>
/// <param name="HasTerminator">Whether the relevant side ends this line with a newline.</param>
internal sealed record ParsedPatchLine(char Prefix, string Text, bool HasTerminator);
