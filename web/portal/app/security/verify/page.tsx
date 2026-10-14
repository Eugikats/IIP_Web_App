import type { Metadata } from "next";
import { Suspense } from "react";
import VerifyPage from "@/components/security/VerifyPage";

export const metadata: Metadata = {
  title: { absolute: "Verify it is you | Insurance Intelligence Platform" },
  robots: { index: false },
};

export default function Page() {
  return (
    <Suspense fallback={null}>
      <VerifyPage />
    </Suspense>
  );
}
