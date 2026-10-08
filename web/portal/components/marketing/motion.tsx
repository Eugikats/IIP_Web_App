"use client";

// Ported from CIP's marketing pages (apps/web/components/marketing): the container, the reveal-on-scroll
// wrapper and the section heading, so IIP and CIP marketing pages share one structure.

import { motion, useReducedMotion } from "framer-motion";
import type { ReactNode } from "react";

const ease = [0.22, 1, 0.36, 1] as const;

export function MarketingContainer({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`mx-auto w-full max-w-7xl px-6 lg:px-10 ${className}`.trim()}>{children}</div>;
}

export function Reveal({ children, className, delay = 0 }: { children: ReactNode; className?: string; delay?: number }) {
  const reduceMotion = useReducedMotion();
  return (
    <motion.div
      initial={reduceMotion ? false : { opacity: 0, y: 18 }}
      whileInView={reduceMotion ? undefined : { opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-80px" }}
      transition={{ duration: 0.55, delay, ease }}
      className={className}
    >
      {children}
    </motion.div>
  );
}

export function SectionHeading({
  eyebrow,
  title,
  description,
  align = "center",
  inverted = false,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  align?: "left" | "center";
  inverted?: boolean;
}) {
  return (
    <Reveal className={align === "center" ? "mx-auto max-w-3xl text-center" : "max-w-3xl"}>
      {eyebrow ? (
        <p className={`m-0 text-xs font-semibold uppercase tracking-[0.14em] ${inverted ? "text-white/60" : "text-[var(--mkt-brand)]"}`}>{eyebrow}</p>
      ) : null}
      <h2 className={`m-0 mt-3 text-balance text-3xl font-bold tracking-tight sm:text-4xl ${inverted ? "text-white" : "text-[var(--mkt-fg)]"}`}>{title}</h2>
      {description ? (
        <p className={`m-0 mt-4 text-base leading-7 sm:text-lg ${inverted ? "text-white/70" : "text-[var(--mkt-muted)]"}`}>{description}</p>
      ) : null}
    </Reveal>
  );
}
