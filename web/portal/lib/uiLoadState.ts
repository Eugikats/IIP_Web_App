export type UiLoadStatus =
  | "loading"
  | "success"
  | "empty"
  | "unauthorized"
  | "forbidden"
  | "not_configured"
  | "offline"
  | "retryable_failure"
  | "terminal_failure";

export type UiLoadState<T> =
  | { status: "loading"; previousData?: T; startedAt: string }
  | { status: "success"; data: T; receivedAt: string }
  | { status: "empty"; data: T; receivedAt: string }
  | { status: Exclude<UiLoadStatus, "loading" | "success" | "empty">; error: UiServiceError; previousData?: T };

type ProblemDetails = {
  code?: unknown;
  type?: unknown;
  title?: unknown;
  detail?: unknown;
  message?: unknown;
  errors?: Record<string, unknown>;
};

function firstProblemMessage(body: ProblemDetails | null, fallback: string): string {
  if (!body) return fallback;
  if (body.errors) {
    for (const value of Object.values(body.errors)) {
      if (Array.isArray(value) && typeof value[0] === "string") return value[0];
    }
  }
  for (const value of [body.detail, body.message, body.title]) {
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return fallback;
}

function responseKind(status: number, message: string): UiServiceError["kind"] {
  if (status === 401) return "unauthorized";
  if (status === 403) return "forbidden";
  if (/not configured|disabled by policy/i.test(message)) return "not_configured";
  if (status === 408 || status === 409 || status === 425 || status === 429 || status >= 500) return "retryable_failure";
  return "terminal_failure";
}

function recoveryFor(kind: UiServiceError["kind"]): string {
  switch (kind) {
    case "unauthorized": return "Sign in again, then return to this workspace.";
    case "forbidden": return "Return to your permitted work or ask a workspace administrator for access.";
    case "not_configured": return "Ask a workspace administrator to configure this capability.";
    case "offline": return "Check the connection. Your filters and safe selection are preserved for retry.";
    case "retryable_failure": return "Try again. Your filters and safe selection are preserved.";
    default: return "Return to the owning workspace and confirm the item is still available.";
  }
}

export class UiServiceError extends Error {
  readonly kind: Exclude<UiLoadStatus, "loading" | "success" | "empty">;
  readonly httpStatus?: number;
  readonly correlationId?: string;
  readonly occurredAt: string;
  readonly recovery: string;
  readonly retryable: boolean;

  constructor(options: {
    kind: UiServiceError["kind"];
    message: string;
    httpStatus?: number;
    correlationId?: string;
    occurredAt?: string;
  }) {
    super(options.message);
    this.name = "UiServiceError";
    this.kind = options.kind;
    this.httpStatus = options.httpStatus;
    this.correlationId = options.correlationId;
    this.occurredAt = options.occurredAt ?? new Date().toISOString();
    this.recovery = recoveryFor(options.kind);
    this.retryable = options.kind === "offline" || options.kind === "retryable_failure";
  }

  static async fromResponse(response: Response, fallback: string): Promise<UiServiceError> {
    let body: ProblemDetails | null = null;
    try {
      body = await response.clone().json() as ProblemDetails;
    } catch {
      // Non-JSON provider responses are reduced to the safe caller fallback.
    }
    const classificationText = body
      ? [body.code, body.type, body.title, body.detail, body.message].filter((value): value is string => typeof value === "string").join(" ")
      : fallback;
    const kind = responseKind(response.status, classificationText || fallback);
    // Server failures may wrap raw provider details. Keep those behind the
    // correlation reference and show approved copy instead.
    const message = response.status >= 500
      ? kind === "not_configured" ? "This capability is not configured for the current workspace." : fallback
      : firstProblemMessage(body, fallback);
    return new UiServiceError({
      kind,
      message,
      httpStatus: response.status,
      correlationId:
        response.headers.get("x-correlation-id") ??
        response.headers.get("request-id") ??
        response.headers.get("traceparent") ??
        undefined,
    });
  }

  static fromUnknown(error: unknown, fallback: string): UiServiceError {
    if (error instanceof UiServiceError) return error;
    const browserOffline = typeof navigator !== "undefined" && navigator.onLine === false;
    const fetchFailure = error instanceof TypeError && /fetch|network|load failed/i.test(error.message);
    const offline = browserOffline || fetchFailure;
    return new UiServiceError({
      kind: offline ? "offline" : "terminal_failure",
      message: browserOffline ? "You appear to be offline." : fallback,
    });
  }
}

export function loadingState<T>(previousData?: T): UiLoadState<T> {
  return { status: "loading", previousData, startedAt: new Date().toISOString() };
}

export function settledState<T>(data: T, empty: boolean): UiLoadState<T> {
  const receivedAt = new Date().toISOString();
  return empty ? { status: "empty", data, receivedAt } : { status: "success", data, receivedAt };
}

export function failedState<T>(error: UiServiceError, previousData?: T): UiLoadState<T> {
  return { status: error.kind, error, previousData };
}
