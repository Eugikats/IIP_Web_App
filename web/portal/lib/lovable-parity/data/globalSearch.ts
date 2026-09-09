import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type GlobalSearchResultType = "Case" | "Document" | "WorkflowTask" | "Approval";

export type GlobalSearchResult = {
  type: GlobalSearchResultType;
  id: string;
  caseId: string | null;
  title: string;
  subtitle: string;
  matchReason: string;
  href: string;
  updatedAt: string;
};

export type GlobalSearchResponse = {
  query: string;
  returnedCount: number;
  hasMore: boolean;
  results: GlobalSearchResult[];
};

export async function loadGlobalSearch(
  query: string,
  role: string,
  userId: string,
  signal?: AbortSignal,
): Promise<GlobalSearchResponse> {
  const response = await fetch(`${apiBaseUrl}/api/v1/search?q=${encodeURIComponent(query)}&limit=20`, {
    headers: scopedHeaders(role, userId),
    signal,
  });

  if (!response.ok) {
    if (response.status === 403) {
      throw new Error("Your current workspace access does not allow global search.");
    }
    throw new Error(`Search failed.`);
  }

  return (await response.json()) as GlobalSearchResponse;
}
