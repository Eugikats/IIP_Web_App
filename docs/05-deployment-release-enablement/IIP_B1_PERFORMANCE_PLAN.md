# Making IIP fast on a B1 App Service plan

Written 2026-10-07, after the bancassurance presentation. The dev plan `asp-iip-dev` (Linux, one instance, hosting both the API and the portal) was scaled up to P1v3 for the presentation and goes back to B1. This plan says how to make B1 feel fast, in the order that pays off most.

## 1. What B1 is, and what that means

B1 is one shared vCPU and 1.75 GB of memory. The API and the portal both run on it, so they compete for the same CPU. Before the presentation the plan sat at 40 to 50 percent CPU and about 80 percent memory when idle, and hit 99 percent under load. Calls took 4 to 20 seconds while the database answered in a few milliseconds: the time was lost waiting for CPU, not waiting for data.

The goal is not to make B1 into P1v3. It is to make each request do far less work, so a small machine has enough.

## 2. What was measured (2026-10-07, on P1v3, warm)

Timed from a laptop, five calls each. About one second of every figure is network distance to Azure, not the server.

| Call | Average | Slowest |
|---|---|---|
| Health check | 1.1 s | 1.3 s |
| Prospects list | 5.0 s | 10.1 s |
| Customers list | 2.0 s | 3.5 s |
| Claims list | 12.7 s | 14.2 s |
| Today's brief | 3.8 s | 12.8 s |
| Channel intelligence | 1.2 s | 1.8 s |
| Escalations | 1.6 s | 3.4 s |
| Course prices | 1.5 s | 3.7 s |

The database itself is not the problem: SQL calls took about 1 ms at the median and 6 ms at the 95th percentile in Application Insights.

The slowest calls are slow on a big server, so a small server will make them worse. They are slow because of how they are written.

## 3. Causes found in the code

1. **One query per row (the claims list).** For every claim it asks the database for the claim's documents, then its audit events, then builds a checklist. 50 claims means about 150 separate queries in one request. This is the 12.7 second call.
2. **Load everything, then filter in memory.** Several lookups read every record in a table and pick one out afterwards: a single prospect lookup reads all prospects, finding a person by phone reads all of them, and the customers list reads all customers and all claims.
3. **Whole history on every read.** Today's brief reads every claim and every audit event, so it gets slower every day the system is used, because the audit log only grows.
4. **Heavy list payloads.** Lists deserialise whole records, including conversation insights and notes, when a table row needs a name, a stage and a score.
5. **Polling.** The live view asks for the whole prospect list every 3 seconds for each open screen. On one shared CPU that is a steady background load.
6. **Almost no caching.** Nothing that rarely changes is cached for even a few seconds: users, products, agent on/off state, knowledge documents. Kato's reply path reads some of these on every message.
7. **Cold starts.** After a restart the first requests pay for JIT compilation, and the API and portal restart each other's neighbour on the shared plan.
8. **The model call.** Kato's reply time is mostly the language model, which no App Service size changes. It is addressed separately (section 5).

## 4. The plan, in order

### Phase 0: settings, about half a day, no code risk

- Turn on HTTP/2 for both apps. (Always On is already on; ARR affinity is already off.)
- Turn on response compression in the API for JSON.
- Publish the API with ReadyToRun and quick tiered compilation so cold starts are shorter.
- Cap the portal's Node memory (for example `NODE_OPTIONS=--max-old-space-size=384`) so it cannot starve the API.
- Point the App Service health check at `/health` so a restarted app is warmed before traffic reaches it.
- Lower Application Insights sampling and log levels in dev.
- Verify: the timing script in section 7 before and after. Expect cold starts to improve, steady calls to move little.

### Phase 1: remove the query-per-row and load-everything patterns, 1 to 2 days, the biggest win

- Claims list: fetch the documents and audit events for all listed claims in two batched queries, then group in memory. About 150 queries become about 4. Expected: 12 s down to well under 2 s.
- Brief: read only recent audit events (or counts by claim) instead of the whole log.
- Prospect, customer and claim lookups by id, phone or thread: add indexed columns (id, phone, thread id, case id) on the hot tables through an EF migration, and query by them instead of scanning every row.
- List endpoints return a slim row (name, stage, score, last contact), not the whole record; the record opens when a row is opened.
- Add paging with a limit of 50 and a "load more" to every list.
- Verify: query counts per request in Application Insights, and the timing script.

### Phase 2: make the live view cheap, half a day

- Replace "fetch the whole list every 3 seconds" with a tiny "has anything changed" call that returns a version number, and fetch the list only when the version moves.
- Poll every 5 to 10 seconds, and only while the screen is visible. Keep 3 seconds for presentation mode only.
- Verify: requests per minute per open screen falls from about 20 to about 6 when nothing changes.

### Phase 3: cache what rarely changes, 1 day

- In-memory cache for 15 to 60 seconds, per workspace, for users, products, agent switches, knowledge document lists and course prices. Clear the entry when it is saved.
- Kato's reply path: read the prospect's record once per message and reuse it for the prompt and the reply.
- Keep the cache small and per workspace so one workspace can never see another's data.

### Phase 4: take the portal off the API's CPU, 1 to 3 days, the structural fix

Choose one, in this order of preference:
1. **Serve the portal's static files from a CDN or storage static site**, and keep only the dynamic pages on the plan. Most of the portal's weight is JavaScript and images that never change between deploys.
2. **A second small plan for the portal.** About double the cost of B1, and it gives the API a CPU of its own. Simple and low risk.
3. **Static export of the portal** to Azure Static Web Apps. Only if the portal does not need server rendering; this must be checked before choosing it.

### Phase 5: the model, ongoing

- Keep Kato's reasoning setting at its default. Lowering it was tried and reverted; it changes how he talks to customers and must be tested on real conversations before any change.
- The insights and triage work already runs after the reply, so it does not slow the customer. Keep it that way.
- Trim the prompt where it repeats itself, and measure the reply time before and after on a fixed set of test messages.
- Consider a smaller model for the background insights agent only, tested for quality first.

## 5. Targets

| Measure | Today (P1v3, from a laptop) | Target on B1 |
|---|---|---|
| Lists (claims, prospects, customers) | 2 to 13 s | under 2 s warm, including network |
| Brief | 3.8 s | under 2 s |
| Idle CPU on the plan | 40 to 50 percent | under 25 percent |
| First request after a restart | 10 to 20 s | under 8 s |
| Requests per minute from one idle open screen | about 20 | about 6 |

## 6. When a demo needs more power

Scale up only for the hours that need it, never close to the start of a demo, and scale down after:

1. Scale up at least a day before, and warm both apps by opening the main screens.
2. Run the timing script to confirm.
3. Scale down once it is over, and say so.

P1v3 costs about USD 0.155 an hour against B1's 0.017.

## 7. How to measure

A timing script (`scripts/local/time-dev-api.sh`) calls the main list endpoints five times each and prints the average and slowest. Run it before and after each phase, on the same plan size, and record the results in this file. Look at Application Insights for query counts per request, not only durations.

## 8. Order of work

Phase 0, then Phase 1, then re-measure. If lists are under 2 seconds on B1, stop and do Phases 2 and 3. Phase 4 only if the portal and API still fight for the CPU.

## 9. Measured on B1

The plan was scaled back to B1 on 2026-10-07 and the same calls were timed with `scripts/local/time-dev-api.sh`, once both apps had finished starting. Same laptop, same five calls each, so these compare directly with section 2.

| Call | P1v3 average | B1 average | B1 slowest |
|---|---|---|---|
| Health check | 1.1 s | 1.8 s | 2.0 s |
| Prospects list | 5.0 s | 4.1 s | 9.7 s |
| Customers list | 2.0 s | 3.8 s | 9.6 s |
| Claims list | 12.7 s | 32.9 s | 60.0 s |
| Today's brief | 3.8 s | 2.8 s | 3.5 s |
| Channel intelligence | 1.2 s | 4.1 s | 11.7 s |
| Escalations | 1.6 s | 1.9 s | 2.3 s |
| Course prices | 1.5 s | 1.8 s | 2.0 s |
| Portal home page | 1.1 s | 1.7 s | 2.0 s |

What this shows:

- Calls that do little work (health, course prices, escalations) cost only a little more on B1. The small server is fine for those.
- Calls that read many records are 2 to 3 times slower on B1, and the claims list is the clear outlier at 33 seconds on average and a full minute at worst. That is the query-per-row pattern in section 3, and it is the first thing to fix.
- The prospects list and channel intelligence have a slow first call (about 10 s) and quicker later ones, which points at work done on every request that a cache would remove.
- Nothing here suggests the database is the limit.

The target figures in section 5 stand. Start with the claims list.

## 10. Measured after the first fixes (2026-10-14, B1, same script, after warm-up)

Changes shipped: the claims list reads documents and audit trails in two queries instead of two per claim; the customers list counts open claims through an index; the prospect list finds each person's customer record through an index on the last nine digits of the number; JSON responses are compressed; the portal's Node memory is capped at 384 MB; .NET tiered PGO and concurrent GC are off on the API. CIP runs on the same B1 size with the same shape of app (one .NET API, one Next.js app) and uses response compression and an in-memory cache; the difference was how IIP reads its data, not the plan.

| Call | Before (B1) | After (B1) |
|---|---|---|
| Prospects list | 39 s average (during load) | 3.0 s |
| Customers list | 3.8 s to 9.3 s | 1.4 s |
| Claims list | 33 s to 56 s | 7 s (warm), 8.6 s average |
| Today's brief | 2.8 s | 2.1 s |
| Channel intelligence | 4.1 s | 1.6 s |
| Health | 1.8 s to 7 s | 1.9 s |

Still over target: the claims list (7 s against under 2 s). It still loads every claim and returns about 330 KB. Next step is paging with a limit of 50 and a slim row, as in Phase 1.

Deploy note: on B1 the API takes longer than the CLI's wait to start, so `az webapp deploy` can report failure after Azure has recorded "Deployment successful". Check `az webapp log deployment show` and `/health/ready` before retrying.
