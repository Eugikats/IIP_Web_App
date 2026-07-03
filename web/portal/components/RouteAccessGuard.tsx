"use client";

import { ReactNode, useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { hasPermission, requiredPermissionForPath } from "@/lib/roleUiConfig";
import { useRole } from "./RoleContext";
import { LoadingState, UnauthorizedState } from "./States";

export function RouteAccessGuard({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { can, isLoadingContext, permissions } = useRole();
  const requiredPermission = requiredPermissionForPath(pathname);
  const shouldOpenClientPortal = pathname === "/" && can("ViewClientPortal") && !can("ViewCommandCenter");

  useEffect(() => {
    if (shouldOpenClientPortal) {
      router.replace("/client-portal");
    }
  }, [router, shouldOpenClientPortal]);

  if (isLoadingContext) {
    return <LoadingState label="Loading runtime access..." />;
  }

  if (shouldOpenClientPortal) {
    return <LoadingState label="Opening client portal..." />;
  }

  if (requiredPermission && !hasPermission(permissions, requiredPermission)) {
    return <UnauthorizedState permission={requiredPermission} />;
  }

  return children;
}
