# AgentKit.Memory.Sqlite.Tests

Verifies `SqliteMemoryStore`, `SqliteDocumentStore`, and `SqliteVectorIndex`:
the three shared conformance suites (including reopen, tombstone, and pointer
persistence), schema and identity binding, cross-instance visibility of
acknowledged writes, record-size bounds that leave prior state intact, honest
exact-scan capability claims, the database-path guard, and registration.
