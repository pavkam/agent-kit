// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AgentKit.Loop.Tests")]
[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Scope = "namespace",
    Target = "~N:AgentKit.Loop",
    Justification = "'AgentKit.Loop' is the normative namespace for the first-party agentic loop " +
        "documented across the agent-runtime architecture; renaming it here alone would make this " +
        "the only inconsistent package in that vocabulary.")]
