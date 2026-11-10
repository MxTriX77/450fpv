## ADDED Requirements

### Requirement: The player measures its own steps
The flight scheduler SHALL time every step's advance with its host clock and keep, per flight, the step count, the steps over 1 ms, the p50, p99 and p99.9 from a fixed-bin histogram, and the max, without any managed allocation. The log's end record SHALL carry them (format 72), the summary reader SHALL print them, and the player SHALL write them to `Player.log` when the flight ends. Timing SHALL never feed the flight's state.

#### Scenario: A flight reports its step times
- **WHEN** a flight ends in the player
- **THEN** `Player.log` and the log's end record give its step count, overruns, p50, p99, p99.9 and max, and a headless replay of the log matches bit for bit

#### Scenario: The timer costs nothing in steady state
- **WHEN** the allocation test flies 60 s with the timer on
- **THEN** it allocates 0 B after the first step

### Requirement: A late flight never builds growing lag
When the scheduler finds itself behind real time by more than a bound B, it SHALL move its time base forward so that it owes at most B, instead of running the whole backlog. Every step SHALL still be a real 1 ms step. The jump SHALL be logged as a hitch with its own cause, how far behind the scheduler was, and how much time it dropped. The pose buffer SHALL follow the new time base. B SHALL be derived from the control chain's latency budget (INP-04) and recorded with its derivation.

#### Scenario: A slow stretch costs one bounded hiccup
- **WHEN** a test clock makes 500 consecutive steps cost 3 ms each
- **THEN** the scheduler never owes more than B, the sticks each step consumes are never older than B, and the log holds the drop hitches with their amounts

#### Scenario: The replay ignores the drop
- **WHEN** a flight with drop hitches is replayed headless
- **THEN** every state record matches bit for bit
