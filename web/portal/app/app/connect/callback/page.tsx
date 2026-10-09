import { Suspense } from "react";
import ConnectCallback from "@/components/files/ConnectCallback";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <ConnectCallback />
    </Suspense>
  );
}
