# 06 — Release 1 workflow execution worker

**What to build:** A real tenant-scoped execution worker consumes agent-workflow messages, re-resolves authority, runs only allow-listed Release 1 tools, writes bounded results and audit, handles idempotent retry/dead-letter, and stops safely at human review. Raw OCR/bytes do not cross the extraction boundary. One bounded live path is proven (or honestly Not configured).

**Blocked by:** 02, 03, 04, 05.

**Status:** in-progress

- [x] Worker + message contract with managed identity and server-resolved scope
- [x] Duplicate, stale version, malformed envelope, provider-unavailable, retry, dead-letter, and human-review-stop behaviors tested
- [x] No raw document/OCR past extraction
- [ ] Dev/staging evidence of one real bounded execution path (or recorded blocker)
