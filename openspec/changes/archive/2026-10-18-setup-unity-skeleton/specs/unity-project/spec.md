# Spec Delta: unity-project

## Purpose
Defines the Unity project every later phase builds in: the pinned editor version, the render pipeline and quality levels, the folder and assembly layout, the boot scene and the batchmode entry points.

## ADDED Requirements

### Requirement: Pinned editor and pipeline
The project SHALL be pinned to Unity 6000.3.26f1 and SHALL render with URP using the Forward+ rendering path at every quality level.

#### Scenario: Version pinned
- **WHEN** `unity/ProjectSettings/ProjectVersion.txt` is read
- **THEN** it names `6000.3.26f1`

#### Scenario: Forward+ at every level
- **WHEN** each quality level's render pipeline asset and its renderer are inspected
- **THEN** each renderer's rendering path is Forward+

### Requirement: Three quality levels
The project SHALL have exactly the quality levels Low, Medium and High, each with its own URP pipeline asset, with High the default for Windows.

#### Scenario: Quality levels present
- **WHEN** the quality settings are read
- **THEN** the levels are Low, Medium and High in that order, and the Windows default is High

### Requirement: Folder and assembly layout
The project SHALL contain `Assets/_Project/{Art,Prefabs,Scenes,Scripts/{App,Input,UI,Feed,World},Shaders,Settings,Editor,Tests}`, and Unity-side scripts SHALL never be referenced by the sim package.

#### Scenario: Layout exists in git
- **WHEN** a fresh clone is listed
- **THEN** every folder above exists (empty folders hold a one-line `README.md`), with its `.meta` file

#### Scenario: Dependency direction
- **WHEN** the assembly definitions are read
- **THEN** no assembly under `Packages/com.fpv450.sim/Runtime` references an assembly under `Assets/`

### Requirement: Boot scene and Windows build
`Fpv450.Build.Windows` SHALL build a Windows x64 player whose only scene is `Boot`, and the player SHALL log that the Boot scene loaded.

#### Scenario: Batchmode build succeeds
- **WHEN** `& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -logFile -` runs
- **THEN** it exits 0 and writes the player to `unity/Builds/Windows/`

#### Scenario: Build failure fails the command
- **WHEN** the build reports any error
- **THEN** the Unity process exits non-zero

#### Scenario: Player opens the Boot scene
- **WHEN** the built player runs with `-batchmode -nographics -smoketest -logFile <file>`
- **THEN** the log contains the Boot-loaded line and the player exits 0 on its own within 30 s

### Requirement: EditMode tests run in batchmode
The EditMode test run SHALL include the sim package's tests and the project's own EditMode assembly, and SHALL pass.

#### Scenario: EditMode run passes
- **WHEN** `& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile -` runs
- **THEN** it exits 0, and the results file lists the `DetMath` test as passed

### Requirement: Noclip camera stub
The Boot camera SHALL carry a noclip controller: mouse look, WASD to move, Shift for fast, E/Q for up and down, and the mouse wheel to change speed, independent of the frame rate.

#### Scenario: Speed is frame-rate independent
- **WHEN** the controller moves for 1 s of simulated time at 30 fps and at 144 fps with the same input
- **THEN** the distances travelled agree within 1 %
