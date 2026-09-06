# Modern C# for AgentKit

Apply these rules whenever a skill creates, changes, or reviews C# code. Target
.NET 10 and C# 14, and prefer current syntax in new code even when neighboring
code is older.

## Async contracts

- Every public asynchronous operation accepts a `CancellationToken` unless it
  cannot wait or perform I/O.
- Return `ValueTask` or `ValueTask<T>` when the operation commonly completes
  synchronously (cache hits, no-op paths, in-memory adapters) or sits on a hot,
  allocation-sensitive abstraction. Implement the synchronous path without an
  async state machine when practical.
- Return `Task` or `Task<T>` when completion is inherently asynchronous, the
  result must support multiple awaits, or callers normally use task combinators
  or cache the returned operation. Do not convert every `Task` mechanically;
  `ValueTask` is a contract and consumption constraint, not decoration.
- Await a `ValueTask` once. Convert with `AsTask()` only at a boundary that
  genuinely requires a `Task`. Never use `.Result`, `.Wait()`, or ambient
  delays.
- Use `IAsyncEnumerable<T>` only for genuine incremental streaming and apply
  `[EnumeratorCancellation]` where the iterator consumes the caller's token.

## Data shapes and value semantics

- Use immutable `record` types for messages, descriptors, options snapshots,
  results, events, and other data whose equality is its contents.
- Use `readonly record struct` for small immutable value objects that need
  generated value equality or `with` support. Use `readonly struct` for small
  hand-authored value types where record-generated API is unnecessary.
- Do not make large, mutable, lifecycle-owning, identity-bearing, or polymorphic
  services structs or records. Use classes for those.
- Prefer current C# syntax when it improves the code: collection expressions,
  pattern matching, switch expressions, `required` members, and the `field`
  keyword. Do not use novelty that obscures a public contract or allocates
  unexpectedly.
- Use primary constructors and positional records for clear immutable data and
  dependency shapes. Use explicit validating constructors or factories when a
  public value must reject invalid input before assignment. Never trade away a
  domain invariant merely to shorten a declaration.

## Static-analysis contracts

- Keep nullable reference types enabled and warnings clean. First express
  compiler-visible contracts with `System.Diagnostics.CodeAnalysis`, including
  `[NotNull]`, `[NotNullWhen]`, `[MaybeNull]`, `[MemberNotNull]`,
  `[DoesNotReturn]`, and `[DoesNotReturnIf]` where their semantics are true.
- Use `JetBrains.Annotations` for additional static-analysis semantics such as
  `[MustUseReturnValue]`, `[InstantHandle]`, `[CollectionAccess]`,
  `[ContractAnnotation]`, or `[PublicAPI]` when the attribute changes a useful
  Rider/ReSharper inspection. Ensure the owning project references the
  annotations package through central package management.
- Prefer the .NET attribute when both ecosystems express the same contract.
  Never add contradictory, speculative, redundant, or warning-suppression-only
  annotations. An annotation is part of the maintained contract.

## C# 14 extension members

Put extensions in a top-level, nongeneric static container and use extension
blocks for all new instance and type extensions:

```csharp
public static class ModelDescriptorExtensions
{
    extension(ModelDescriptor descriptor)
    {
        public bool SupportsStreaming =>
            descriptor.Capabilities.Contains(ModelCapability.Streaming);

        public ValueTask ValidateAsync(CancellationToken cancellationToken) =>
            descriptor.Validator.ValidateAsync(descriptor, cancellationToken);
    }

    extension(ModelDescriptor)
    {
        public static ModelDescriptor Empty { get; } = new([], []);
    }
}
```

A named receiver declares instance extension members; a type-only receiver
declares static extension members. Keep the classic
`Method(this Receiver receiver, ...)` form only when a verified consumer,
compiler, generator, analyzer, or interop boundary cannot consume C# 14
extension metadata, and record that reason next to the code or in its design
documentation.
