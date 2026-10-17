# sim-core-build Specification

## Purpose
Defines how the engine-free simulation core is packaged and built, so the same sources compile inside Unity and in plain .NET tools for tests, benchmarks and replay.

## Requirements

### Requirement: Engine-free package
The sim core SHALL live in the embedded package `com.fpv450.sim` under `unity/Packages/`, and its runtime assembly SHALL NOT reference any Unity engine assembly.

#### Scenario: Runtime assembly has no engine references
- **WHEN** the runtime assembly definition of `com.fpv450.sim` is read
- **THEN** it sets `noEngineReferences` to true and lists no `UnityEngine` or `UnityEditor` references

#### Scenario: No Unity namespace in runtime sources
- **WHEN** every `.cs` file under the package's `Runtime/` folder is searched for `using UnityEngine` or `using UnityEditor`
- **THEN** there are zero matches

### Requirement: Same sources outside Unity
The .NET tools SHALL compile the package's runtime sources directly from `unity/Packages/com.fpv450.sim/Runtime/`, never from a copy.

#### Scenario: Tests pass without Unity
- **WHEN** `dotnet test tools/simtests` runs on a machine with the pinned .NET SDK and no Unity editor running
- **THEN** the build succeeds with warnings treated as errors, and at least one `DetMath` test passes

#### Scenario: A new runtime file is picked up automatically
- **WHEN** a new `.cs` file is added anywhere under the package's `Runtime/` folder
- **THEN** the next `dotnet build` of `tools/simcore` compiles it without any project-file edit

### Requirement: API surface Unity accepts
The runtime sources SHALL compile against .NET Standard 2.1 at the C# language version Unity 6 supports, so code that builds in the tools also builds in Unity.

#### Scenario: Core library targets netstandard2.1
- **WHEN** `dotnet build tools/simcore -c Release` runs
- **THEN** it builds a `netstandard2.1` assembly at the pinned language version with no errors

### Requirement: Bench entry points exist
`tools/worldbench` and `tools/flightbench` SHALL accept `--golden` and `--bench` and exit 0 when no cases are registered, printing that none are registered.

#### Scenario: Worldbench golden with no cases
- **WHEN** `dotnet run -c Release --project tools/worldbench -- --golden` runs
- **THEN** it prints that zero golden cases are registered and exits with code 0

#### Scenario: Unknown argument
- **WHEN** either bench is run with an unknown argument
- **THEN** it prints its usage and exits with a non-zero code
