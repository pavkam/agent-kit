# Composition and configuration

**Role:** Build a valid agent from independently replaceable components.

Composition is the only place where the complete object graph is known. It turns
host registrations and configuration into an immutable agent definition, then
creates an isolated graph of run-owned services when execution starts.

## Responsibilities

This component owns:

- dependency-injection registration and startup validation;
- immutable agent definitions and named component references;
- configuration discovery, precedence, merge behavior, and snapshots;
- service lifetimes, ownership, and disposal;
- deterministic selection among keyed or named implementations; and
- explicit replacement of defaults.

It does not own run policy, model selection, tool authorization, persistence, or
provider behavior. Those services are selected here and act within their own
contracts. Runtime components do not reach back into the container to discover
dependencies.

## Agent definition and run scope

An agent definition is a reusable blueprint. It identifies the loop, models,
context contributors, tool sources, policies, stores, output contract, limits,
and extensions that form an agent. It is immutable after validation and safe to
share between concurrent runs.

A run scope contains mutable execution state: current turn, budget reservations,
run-scoped extension state, request snapshots, and cancellation. Nothing in the
run scope leaks into another run. Session state is loaded through the session
component; it is not kept indefinitely in a singleton agent object.

## Registration model

Registrations are either singular or additive. Singular services, such as the
default loop or session store, have one effective implementation and an explicit
replacement path. Additive services, such as context contributors, tool
providers, and observers, retain deterministic ordering and reject ambiguous
keys.

Repeated registration has documented idempotency. Registration never builds a
nested container or resolves services early. Startup validation catches missing
keys, duplicate aliases, invalid scopes, impossible limits, and incompatible
capabilities before the first run.

## Configuration model

Configuration is layered immutable input. Library defaults, host settings,
managed policy, workspace settings, agent definition, composed capabilities, run
options, and next-turn overrides have explicit precedence. Each value also
declares how it combines: replacement, append, keyed merge, deep merge, ordered
rules, or explicit reset.

Security constraints are not ordinary overridable values. Untrusted workspace
configuration cannot load executable extensions, inject credentials, or widen
tool, filesystem, network, or model authority. Invalid reloads leave the last
known-good snapshot active. In-flight work continues with its captured snapshot.

Credentials are resolved by dedicated leaf integrations at the moment they are
needed. They never become part of an agent definition, options display, context
manifest, message, or durable record.

## Lifetimes

Immutable definitions, descriptors, and thread-safe catalogs may be shared. Run
state and mutable capability instances are run-scoped. Operation adapters may be
transient when the container owns their disposal. Every public component
documents whether it is thread-safe, what it owns, and when it is disposed.

## Related concept specifications

- [Agent definition and run context](../concepts/agent-definition-and-run-context.md)
- [Configuration and overrides](../concepts/configuration-and-overrides.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
