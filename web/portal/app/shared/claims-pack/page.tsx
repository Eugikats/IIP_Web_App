import { Suspense } from "react";
import Component from "@/lovable-pages/SharedClaimsPack";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Component />
    </Suspense>
  );
}
