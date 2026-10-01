// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Remembers one applied publication so an equivalent replay returns the stored version.</summary>
/// <param name="Key">The publication's idempotency key.</param>
/// <param name="Fingerprint">The canonical fingerprint of the publication's version, content, chunk set, and activation flag.</param>
/// <param name="Version">The version the publication stored.</param>
internal sealed record DocumentWriteReceipt(string Key, string Fingerprint, DocumentVersion Version);
