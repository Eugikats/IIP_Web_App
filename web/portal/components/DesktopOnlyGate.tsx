"use client";

import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { EmptyState } from "@/components/States";

type DesktopOnlyGateProps = {
  title: string;
  description: string;
  children: ReactNode;
};

export function DesktopOnlyGate({ title, description, children }: DesktopOnlyGateProps) {
  const [isDesktop, setIsDesktop] = useState(false);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(min-width: 1024px)");
    const update = () => setIsDesktop(mediaQuery.matches);

    update();
    mediaQuery.addEventListener("change", update);
    return () => mediaQuery.removeEventListener("change", update);
  }, []);

  if (!isDesktop) {
    return <EmptyState title={title} description={description} />;
  }

  return <>{children}</>;
}
