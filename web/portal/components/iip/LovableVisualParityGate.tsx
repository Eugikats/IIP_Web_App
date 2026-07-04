import type { ReactNode } from "react";
import { LOVABLE_VISUAL_PARITY_MODE, LOVABLE_VISUAL_PARITY_MODE_NAME } from "@/lib/lovable-parity/mode";

export default function LovableVisualParityGate({ children }: { children: ReactNode }) {
  if (process.env.NODE_ENV === "production" && !LOVABLE_VISUAL_PARITY_MODE) {
    return (
      <main className="min-h-screen bg-white px-6 py-10 text-slate-950">
        <div className="mx-auto max-w-2xl rounded-lg border border-slate-200 bg-slate-50 p-6">
          <p className="text-sm font-semibold">{LOVABLE_VISUAL_PARITY_MODE_NAME} disabled</p>
          <p className="mt-2 text-sm text-slate-600">
            Set NEXT_PUBLIC_VISUAL_PARITY_MODE=true only for the approved development visual-parity migration.
          </p>
        </div>
      </main>
    );
  }

  return children;
}
