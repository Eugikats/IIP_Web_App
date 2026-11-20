"use client";

import { Suspense } from "react";
import WorkspacesDirectory from "@/components/platform/WorkspacesDirectory";
import { PlatformLoadingState } from "@/components/platform/ui";

export default function Page() {
  return (
    <Suspense fallback={<PlatformLoadingState title="Loading…" />}>
      <WorkspacesDirectory />
    </Suspense>
  );
}
