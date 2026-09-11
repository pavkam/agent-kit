# Recorded JSON Schema validation cases

These unchanged fixtures come from the MIT-licensed
[JSON Schema Test Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite/tree/f6fd52a0a95472e079cbfc6ef7f089702b80e045/tests/draft2020-12),
pinned at commit `f6fd52a0a95472e079cbfc6ef7f089702b80e045`. The accompanying
`LICENSE` is copied unchanged.

`CompiledToolSchemaTests` runs every case in these fourteen files through the
public schema-engine registration and typed compiled handle. They cover the
selected supported draft 2020-12 keywords; they are not a claim of complete
dialect conformance. Applicator and uniqueness behavior has focused local cases,
including unsupported-keyword rejection. Tests use only these local recorded
files and perform no network access.
