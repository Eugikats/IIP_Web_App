import { Suspense } from "react";
import Courses from "@/components/academy/pages/Courses";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <Courses />
    </Suspense>
  );
}
