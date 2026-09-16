// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

/// <summary>A presentation source variant not recognized by any formatter, used to exercise defensive fallbacks.</summary>
internal sealed record UnsupportedPresentationSource: ToolPresentationSource;
