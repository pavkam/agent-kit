// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares whether observation exporters may capture classified payload bytes.</summary>
/// <param name="Enabled">When <see langword="false"/>, sinks export structural facts only.</param>
public sealed record ObservationContentCapturePolicy(bool Enabled = false);
