# Structured output

**Status:** Normative

**Architecture:** [Structured output](../architecture/structured-output.md)

**Depends on:** [Messages](message-and-content-model.md),
[model capabilities](model-providers-and-capabilities.md),
[tool scheduling](tool-scheduling-and-concurrency.md)

## Purpose

The first-party processing boundary is defined by the
[structured-output architecture](../architecture/structured-output.md).

Structured output turns model output into validated application data. It is not
equivalent to asking politely for JSON.

## Output modes

AgentKit MUST represent at least:

- **text output:** validated or unvalidated text;
- **tool output:** a synthetic application tool call carrying the result;
- **native output:** provider-enforced schema response;
- **prompted output:** schema/instructions with local parsing and validation;
- **media output:** typed image, audio, file, or provider-native result; and
- **union output:** one of several named schemas with unambiguous selection.

[Capability negotiation](model-providers-and-capabilities.md) chooses a
supported mode or fails or downgrades according to explicit policy. A
native-schema claim MUST include the provider's supported JSON Schema dialect
and restrictions.

## Output contract

The canonical `OutputDefinition` declaration lives in the
[structured-output architecture](../architecture/structured-output.md#normative-minimal-contract-shape).
This specification defines its required behavior; feature packages and provider
adapters MUST reuse that contract rather than introduce local output
definitions. Its identity/version, name, explicit mode, optional schema and CLR
runtime type, union alternatives, ordered validator references, validation and
retry policies, and `OutputEndStrategy` are one immutable value resolved before
provider I/O.

A declarative JSON schema without a CLR runtime type yields validated JSON, not
a magically safe application object. Deserialization into a runtime type MUST
occur after schema/protocol validation and follow configured serializer limits.

## Validation pipeline

Candidate output MUST pass:

1. protocol/mode validation;
2. bounded parse or provider-native item validation;
3. canonical schema validation;
4. runtime-type deserialization when requested;
5. ordered synchronous/asynchronous output validators; and
6. final output middleware.

Validators receive immutable run context and may accept, transform within the
declared type/schema, or request a model retry. They MUST NOT perform hidden
side effects unless explicitly modeled as tools.

## Retry behavior

Output-validation retries use a
[budget separate from tool retries](usage-limits-and-budgets.md). A retry
creates a corrective model input identifying the validation issue safely and
preserving the original causal response. Exhaustion ends with
`OutputValidationFailed` and all attempts remain observable.

Partial streaming output MAY be exposed as provisional values. Only the final
validated value is application output. Consumers MUST NOT be told that a partial
object is stable or schema-valid unless the validator explicitly supports
incremental validity.

## Tool-output end strategies

When a response mixes output-tool and function-tool calls, the runtime MUST
apply the `OutputDefinition.EndStrategy` value using these named strategies:

| Strategy   | Output calls                           | Function calls                  | Winner                             |
| ---------- | -------------------------------------- | ------------------------------- | ---------------------------------- |
| Graceful   | Validate in source order until success | Run according to schedule       | First valid output in source order |
| Early      | Stop at first valid output             | Skip calls not already required | First valid output encountered     |
| Exhaustive | Validate all                           | Run all according to schedule   | First valid output in source order |

Failed output validation means another output candidate or model retry may
continue. The [tool scheduler](tool-scheduling-and-concurrency.md) applies the
chosen strategy before avoidable side effects start. Already running tools
follow cancellation and settlement policy.

Ordinary assistant text alongside output tool calls MUST NOT preempt them unless
the output definition explicitly accepts text as a union alternative.

## Synthetic tool isolation

An output tool is a protocol mechanism, not an application side-effecting tool.
It MUST NOT pass through external permission approval as though it could mutate
resources, but it does pass schema, bounds, validation, correlation, and retry
logic. Its reserved provider-visible name must not collide with application
tools.

## Acceptance scenarios

- Invalid native/provider output still fails local canonical validation.
- Tool and output retry budgets cannot consume each other.
- Exhaustive execution returns the first valid output by source order even when
  a later call finishes first.
- A plain JSON schema returns a validated JSON value unless a runtime type is
  declared.
- Partial stream values never become final before terminal validation.
- Output-tool names cannot collide with application tool aliases.

## Related specifications

- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
