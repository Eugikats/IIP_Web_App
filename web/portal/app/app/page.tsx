import { Suspense } from "react";
import Component from "@/lovable-pages/Brief";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Component />
    </Suspense>
  );
}
