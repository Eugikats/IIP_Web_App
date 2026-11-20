"use client";

import { Suspense } from "react";
import WorkspaceWizard from "@/components/platform/WorkspaceWizard";
import { PlatformLoadingState } from "@/components/platform/ui";

export default function Page() {
  return (
    <Suspense fallback={<PlatformLoadingState title="Loading…" />}>
      <WorkspaceWizard />
    </Suspense>
  );
}
