import { Suspense } from "react";
import Academy from "@/components/academy/AcademyCatalogue";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Academy />
    </Suspense>
  );
}
