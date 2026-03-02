# Type subsumption cache causes sustained ~150% CPU in IDE with generative type providers (hash code not memoized)

## Description

The type subsumption cache introduced in PR #18875 and partially fixed in PR #18926 causes sustained high CPU usage (~150%) in IDE/FSAC when a project uses generative type providers (for example, a custom HTML type provider with ~15 template instantiations).

PR #18926 memoized `TypeStructure` creation via a `ConditionalWeakTable`, but `TTypeCacheKey.GetHashCode` still calls `GenericHashArbArray` on the underlying `ImmutableArray<TypeToken>` on every cache lookup. Since `ImmutableArray<T>` is a struct wrapping `T[]`, F# structural hashing delegates to the underlying array, making each hash call O(n) over the token array. In IDE mode with repeated type checks, this dominates CPU time.

A 10-second `dotnet-trace` CPU sample on the FSAC process (166% CPU) shows:

| Samples | Function |
|---------|----------|
| 2414 | `Thread.PollGC` |
| 1170 | `RuntimeTypeHandle.InternalAllocNoChecks` |
| 820 | `GenericEqualityArbArray` |
| 696 | `TTypeCacheKey.GetHashCode` -> `GenericHashArbArray` |
| 96 | `ConcurrentDictionary.GrowTable` |
| 12 | `Cache.rebuildStore` |

## Repro steps

1. Create an F# solution targeting `net10.0` (`LangVersion` 10) with 5+ projects.
2. Add a generative type provider (for example, one producing ~15 provided types with a few methods each).
3. Reference the type provider from 2+ projects in the solution.
4. Open the solution in VS Code with Ionide (FSAC).
5. Observe sustained ~150% CPU on the `dotnet fsautocomplete.dll` process that does not settle down.

A minimal repro with 30 template instantiations in a single project did not reproduce the issue. The combination of multiple projects, generative type provider types, and other type providers (for example, `FSharp.Data.JsonProvider`) in the same solution appears necessary to trigger it.

## Expected behavior

CPU usage should settle to near-zero after initial type checking completes. The subsumption cache should improve performance, not degrade it.

## Actual behavior

CPU stays at ~150% indefinitely. The CPU is spent almost entirely in `TTypeCacheKey.GetHashCode` -> `GenericHashArbArray`, hashing `ImmutableArray<TypeToken>` arrays on every cache lookup. The cache appears to continuously grow and rebuild, and each rebuild re-hashes all existing entries at O(n) per entry.

Diagnostic logging in the type provider confirmed:

- FCS re-creates provider instances every ~10 seconds (observed 12 instances in ~60 seconds).
- Even with static caches returning identical `ProvidedTypeDefinition` objects (all cache hits), CPU remains high.
- The provider itself does negligible work; CPU is spent in FCS internal processing.

## Known workarounds

Set `<LangVersion>9.0</LangVersion>` in `Directory.Build.props` to disable the `UseTypeSubsumptionCache` language feature. This eliminates the CPU issue entirely. This requires replacing F# 10-only syntax where needed (for example, `let! x: T =` -> `let! (x: T) =`).

## Suggested fix

Cache the hash code inside `TypeStructure` (or `TTypeCacheKey`) so `GetHashCode` is O(1) after first computation, instead of O(n) on every call.

## Related information

- macOS (Darwin 25.3.0, Apple Silicon)
- .NET SDK 10.0.103
- Ionide 7.31.1
- FSAC 0.83.0
- FSharp.Compiler.Service 43.10.101-servicing
- Related items:
  - PR #18875 (type subsumption cache introduction)
  - Issue #18925
  - PR #18926 (partial fix)
  - Issue #19169 (build regression)
