"use client";

import { useParams } from "next/navigation";
import AutomationDetail from "@/components/platform/AutomationDetail";

export default function Page() {
  const params = useParams<{ id: string }>();
  return (
    <AutomationDetail id={params?.id ?? ""} />
  );
}
