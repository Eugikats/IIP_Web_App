import type { CSSProperties } from "react";

// The design's person avatar: coloured initials, the colour chosen from the name so the same person is always the same colour.
const COLS = ["#1E4FD8", "#F08A24", "#0B6E6E", "#7A2E8C", "#B4232A", "#0F7A3E", "#334155"];

export function Avatar({ name, size = 40 }: { name: string; size?: number }) {
  const initials = name.split(" ").filter(Boolean).map((part) => part[0]).slice(0, 2).join("").toUpperCase() || "?";
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) % 997;
  const style: CSSProperties = { width: size, height: size, fontSize: size * 0.36, background: COLS[hash % COLS.length] };
  return <div className="av" style={style} aria-hidden="true">{initials}</div>;
}
