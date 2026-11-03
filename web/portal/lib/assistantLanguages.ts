import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type AssistantLanguageOption = {
  code: string;
  name: string;
  translationQuality: "Good" | "Moderate" | "Basic";
  enabled: boolean;
  voiceInput: "Available" | "Not configured";
  voiceOutput: "Available" | "Not configured";
};

export type AssistantLanguageSettings = {
  languages: AssistantLanguageOption[];
  enabledCodes: string[];
  translationProviderStatus: "NotConfigured" | "Healthy" | "Failing" | string;
  allowUnreviewedTranslations: boolean;
  configurationSource: "Deployment default" | "Workspace" | string;
};

export type AssistantLanguageResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function safeError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (body.message) return body.message;
    const first = body.errors ? Object.values(body.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // Keep the user-facing error independent of provider and infrastructure details.
  }
  return "Language settings could not be saved. Check your connection and try again.";
}

export async function getAssistantLanguages(role: string): Promise<AssistantLanguageResult<AssistantLanguageSettings>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/assistant-languages`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response) };
    return { ok: true, value: (await response.json()) as AssistantLanguageSettings };
  } catch {
    return { ok: false, error: "Language settings could not be loaded. Check your connection and try again." };
  }
}

export async function saveAssistantLanguages(
  role: string,
  enabledCodes: string[],
  reason: string,
): Promise<AssistantLanguageResult<{ enabledCodes: string[]; configurationSource: string }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/assistant-languages`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": reason, ...scopedHeaders(role, "") },
      body: JSON.stringify({ enabledCodes }),
    });
    if (!response.ok) return { ok: false, error: await safeError(response) };
    return { ok: true, value: (await response.json()) as { enabledCodes: string[]; configurationSource: string } };
  } catch {
    return { ok: false, error: "Language settings could not be saved. Check your connection and try again." };
  }
}
