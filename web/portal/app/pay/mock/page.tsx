import { Suspense } from "react";
import Component from "@/lovable-pages/MockCardPage";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Component />
    </Suspense>
  );
}
