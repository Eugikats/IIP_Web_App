"use client";

import { useParams } from "next/navigation";
import WorkspaceDetail from "@/components/platform/WorkspaceDetail";

export default function Page() {
  const params = useParams<{ id: string }>();
  return (
    <WorkspaceDetail id={params?.id ?? ""} />
  );
}
