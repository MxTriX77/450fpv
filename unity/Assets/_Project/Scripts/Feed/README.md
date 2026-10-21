The analog PAL goggles feed and the OSD rendering (tech-artist, phase 4).

- `Core/` (`Fpv450.Feed.Core`, engine-free): `FeedEvents` and `FeedLoss` ported from the Godot feed, `IFeedSignals` with `StubFeedSignals`, and `FeedSchedule` (the schedule dump and digest). `tools/feedcore` compiles the same files, `tools/feedbench schedule --seed <n>` dumps a seed's schedule, and `tools/feedtests` runs `Tests/EditMode/Feed/` outside Unity.
