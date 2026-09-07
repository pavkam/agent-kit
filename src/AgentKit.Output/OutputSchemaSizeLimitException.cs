// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Stops deterministic schema serialization after its configured byte bound is exceeded.</summary>
internal sealed class OutputSchemaSizeLimitException: Exception;
