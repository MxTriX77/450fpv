# Tasks: setup-sim-tools

## 1. Sim package and .NET tools

- [x] 1.1 [physics-engineer] Create the `com.fpv450.sim` package (package.json, Runtime asmdef with `noEngineReferences: true`, Tests asmdef, `DetMath` with its rules header and one test) and verify `grep -r "using Unity" unity/Packages/com.fpv450.sim/Runtime` finds nothing
- [x] 1.2 [physics-engineer] Add `tools/Directory.Build.props`, `tools/simcore`, `tools/simtests`, `tools/worldbench` and `tools/flightbench` per design, and verify `dotnet test tools/simtests`, `dotnet run -c Release --project tools/worldbench -- --golden`, `-- --bench` and `dotnet run -c Release --project tools/flightbench -- --bench` all exit 0

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
