// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports caller cancellation before a read handle was returned.</summary>
public sealed record FileReadOpenCancelled: FileReadOpenResult;
