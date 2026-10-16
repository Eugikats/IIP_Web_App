import { Suspense } from "react";
import PaymentCallbackPage from "@/components/academy/pages/PaymentCallbackPage";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <PaymentCallbackPage />
    </Suspense>
  );
}
