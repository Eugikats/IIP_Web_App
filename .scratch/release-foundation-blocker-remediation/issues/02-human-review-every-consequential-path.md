# 02 — Human review on every consequential workflow path

**What to build:** Publishing a workflow fails unless every executable path that performs a consequential or external action reaches a human-review gate before completion (including branches, cycles, and invalid alternate Completes). Agent/system actors cannot Decide/Send/Execute consequential actions. The canonical Release 1 graph still publishes.

**Blocked by:** None — can start immediately.

**Status:** done (branch `harry/rf-human-review-every-path`)

- [x] Publish validator enforces all-paths human-review invariant (not “some path exists”)
- [x] Platform ceiling blocks non-human consequential Decide/Send/Execute
- [x] Unsafe graphs rejected with clear reasons; canonical Release 1 graph publishes
- [x] API/contract tests cover bypass, branch, cycle, and unreachable consequential cases
