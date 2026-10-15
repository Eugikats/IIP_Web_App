"use client";

import Link from "next/link";
import { Icon } from "@/components/academy/Icon";

/** The states a page needs besides its content: loading, empty, error, not available, no permission. In the design's look. */
export function PageState({
  kind, title, text, action,
}: {
  kind: "error" | "empty" | "forbidden" | "notfound" | "unavailable";
  title: string;
  text?: string;
  action?: { label: string; href: string };
}) {
  const icon = kind === "error" || kind === "unavailable" ? "refresh" : kind === "forbidden" ? "lock" : kind === "notfound" ? "search" : "book";
  return (
    <section className="card panel state" role={kind === "error" ? "alert" : "status"} data-testid={`state-${kind}`}>
      <div className="nic" style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--blue-2)", color: "var(--blue)", display: "grid", placeItems: "center", margin: "0 auto 14px" }}>
        <Icon name={icon} size={26} />
      </div>
      <h2>{title}</h2>
      {text ? <p>{text}</p> : null}
      {action ? <Link className="btn primary" href={action.href}>{action.label}</Link> : null}
    </section>
  );
}

export function SkeletonCards({ count = 3 }: { count?: number }) {
  return (
    <div className="grid3" aria-busy="true" aria-label="Loading">
      {Array.from({ length: count }, (_, index) => <div key={index} className="skel" style={{ height: 320 }} />)}
    </div>
  );
}
