// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of one <see cref="DocumentWriteReceipt"/>.</summary>
/// <param name="Key">The publication key.</param>
/// <param name="Fingerprint">The publication fingerprint.</param>
/// <param name="Version">The stored version text.</param>
internal sealed record DocumentReceiptDocument(string Key, string Fingerprint, string Version);
