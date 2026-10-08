"use client";

// Ported from CIP's MarketingNav: brand on the left, section links, Sign in and Sign up on the right, and a
// menu on small screens. Links are absolute so the bar works from every public page.

import Link from "next/link";
import { useEffect, useState } from "react";
import { Menu, X } from "lucide-react";
import { MarketingContainer } from "./motion";

const LINKS = [
  { href: "/#products", label: "Products" },
  { href: "/#capabilities", label: "Capabilities" },
  { href: "/#how-it-works", label: "How it works" },
  { href: "/#trust", label: "Security" },
] as const;

export function MarketingBrandLink({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <Link href="/" onClick={onNavigate} className="flex min-w-0 items-center gap-2.5" aria-label="Insurance Intelligence Platform home">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src="/iip-mark.png" alt="" className="h-8 w-auto shrink-0 object-contain" />
      <span className="hidden truncate text-sm font-bold tracking-tight text-[var(--mkt-fg)] min-[420px]:inline">Insurance Intelligence Platform</span>
    </Link>
  );
}

export default function MarketingNav() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <header className="sticky top-0 z-50 border-b border-[var(--mkt-border)] bg-white pt-[env(safe-area-inset-top)]">
      <MarketingContainer>
        <div className="flex h-16 items-center justify-between gap-3">
          <MarketingBrandLink onNavigate={() => setOpen(false)} />
          <nav className="hidden items-center gap-1 lg:flex" aria-label="Primary">
            {LINKS.map((link) => (
              <Link key={link.href} href={link.href} className="rounded-md px-3 py-2 text-sm font-medium text-[var(--mkt-muted)] transition-colors hover:bg-[var(--mkt-brand-soft)] hover:text-[var(--mkt-brand)]">
                {link.label}
              </Link>
            ))}
          </nav>
          <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
            <Link href="/login" className="hidden rounded-[6px] px-3 py-2 text-sm font-semibold text-[var(--mkt-fg)] transition-colors hover:bg-[var(--mkt-brand-soft)] sm:inline-flex">
              Sign in
            </Link>
            <Link href="/signup" className="inline-flex shrink-0 whitespace-nowrap rounded-[6px] bg-[var(--mkt-brand)] px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-[#1258d6] sm:px-3.5">
              Sign up
            </Link>
            <button
              type="button"
              className="inline-flex shrink-0 items-center justify-center rounded-md p-2 text-[var(--mkt-fg)] hover:bg-[var(--mkt-brand-soft)] lg:hidden"
              aria-label={open ? "Close navigation" : "Open navigation"}
              aria-expanded={open}
              onClick={() => setOpen((value) => !value)}
            >
              {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>
          </div>
        </div>
      </MarketingContainer>
      {open ? (
        <div className="border-t border-[var(--mkt-border)] bg-white lg:hidden">
          <MarketingContainer>
            <nav className="flex flex-col gap-1 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]" aria-label="Mobile">
              {LINKS.map((link) => (
                <Link key={link.href} href={link.href} onClick={() => setOpen(false)} className="rounded-lg px-3 py-3 text-sm font-semibold text-[var(--mkt-fg)] hover:bg-[var(--mkt-brand-soft)]">
                  {link.label}
                </Link>
              ))}
              <Link href="/login" onClick={() => setOpen(false)} className="mt-1 rounded-lg px-3 py-3 text-sm font-semibold text-[var(--mkt-brand)] hover:bg-[var(--mkt-brand-soft)]">
                Sign in
              </Link>
            </nav>
          </MarketingContainer>
        </div>
      ) : null}
    </header>
  );
}
