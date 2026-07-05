import type { ReactNode } from "react";

/**
 * Historically this gate blocked the entire app in production unless the
 * LovableVisualParity dev flag was set — a safety net while `/app/*` screens
 * still rendered prototype fixtures. That prerequisite is now met: every screen
 * either fetches real `/api/v1` data or shows an honest empty/"Not configured"
 * state, and prototype fixtures render only when NEXT_PUBLIC_VISUAL_PARITY_MODE
 * is true (per-screen). So the whole-app block is no longer needed; per-screen
 * honesty is the protection. Kept as a thin pass-through so the layout wiring is
 * unchanged and a future global gate has a home if ever needed.
 */
export default function LovableVisualParityGate({ children }: { children: ReactNode }) {
  return children;
}
