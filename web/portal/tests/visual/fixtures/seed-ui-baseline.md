# Gate 0.2 Visual Fixture Strategy

These fixtures are synthetic and deterministic. They are used only by Playwright
route interception in `tests/visual/fixtures/iip-fixtures.ts`.

The fixture data intentionally uses:

- `.example.invalid` email addresses.
- Synthetic tenant/workspace IDs.
- Synthetic claim, document, approval, task and audit identifiers.
- `NotConfigured` provider/channel states unless a screen needs a healthy state
  in a future gate.

These screenshots are diagnostic only. They are not approved final visual
baselines and must not be used as production evidence.

Customer portal routes are intentionally excluded until customer-safe DTOs and
routes exist.
