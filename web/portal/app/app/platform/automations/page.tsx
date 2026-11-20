"use client";

import { Suspense } from "react";
import AutomationsView from "@/components/platform/AutomationsView";
import { PlatformLoadingState } from "@/components/platform/ui";

export default function Page() {
  return (
    <Suspense fallback={<PlatformLoadingState title="Loading…" />}>
      <AutomationsView basePath="/app/platform/automations" />
    </Suspense>
  );
}
