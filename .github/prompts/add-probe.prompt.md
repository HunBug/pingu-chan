# Add a New Probe

Goal: implement a new periodic active test (Probe) consistent with DESIGN.md.

Steps
1) Create a class `YourProbe` implementing the `IProbe` contract (see DESIGN.md "Core Interfaces"). Place under `src/PinguChan.Core/Probes/YourProbe.cs`.
2) Implement async network logic with short timeouts and cancellation support. Return a `NetSample` with `Kind`, `Target`, `Ok`, and optional `Milliseconds`/`Extra` JSON.
3) Wire the probe into the scheduler/registration point used by the CLI `run` command.
4) Update `pingu.yaml` with the probe interval and targets, keeping durations reasonable.
5) Add unit tests in `tests/PinguChan.Tests/` for success, timeout, and error paths.
6) Document any new config keys in README.

References
- Design contracts: [DESIGN.md](../../DESIGN.md)
