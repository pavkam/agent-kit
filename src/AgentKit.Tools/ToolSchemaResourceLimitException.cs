// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Unwinds bounded local schema work to its typed resource-limit outcome without exposing content.</summary>
/// <remarks>This internal signal never leaves the schema engine or compiled-handle operation.</remarks>
internal sealed class ToolSchemaResourceLimitException: Exception;
