import { Suspense } from "react";
import Component from "@/lovable-pages/ForgotPassword";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Component />
    </Suspense>
  );
}
