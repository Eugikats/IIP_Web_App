"use client";

import { Suspense } from "react";
import AutomationWizard from "@/components/platform/AutomationWizard";
import { PlatformLoadingState } from "@/components/platform/ui";

export default function Page() {
  return (
    <Suspense fallback={<PlatformLoadingState title="Loading…" />}>
      <AutomationWizard />
    </Suspense>
  );
}
