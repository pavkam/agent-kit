// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Records one query rewriter registration with its descriptor and concrete type, so profiles can capture its exact version.</summary>
/// <param name="Descriptor">The rewriter's declared key and version.</param>
/// <param name="RewriterType">The concrete rewriter type.</param>
internal sealed record QueryRewriterDeclaration(QueryRewriterDescriptor Descriptor, Type RewriterType);
