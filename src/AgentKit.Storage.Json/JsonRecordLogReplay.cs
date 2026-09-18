// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Reports the acknowledged records recovered from one record log and whether a torn trailing append was found.</summary>
/// <param name="Records">The ordered acknowledged record payloads, each excluding its terminating newline.</param>
/// <param name="HasIncompleteTrailingRecord">
/// Whether the log ended without a terminating newline. A torn tail is an append that was never acknowledged, so an adapter
/// in a recovery-permitting mode may discard it; an adapter restricted to validation must refuse to proceed.
/// </param>
/// <remarks>The payload memories alias one replay buffer and remain valid only while the replay result is reachable.</remarks>
public readonly record struct JsonRecordLogReplay(
    IReadOnlyList<ReadOnlyMemory<byte>> Records,
    bool HasIncompleteTrailingRecord);
