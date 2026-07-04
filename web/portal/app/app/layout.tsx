import AppShell from "@/components/iip/AppShell";
import type { ReactNode } from "react";

export default function LovableAppLayout({ children }: { children: ReactNode }) {
  return <AppShell>{children}</AppShell>;
}