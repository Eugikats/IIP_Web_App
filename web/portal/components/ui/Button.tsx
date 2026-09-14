// Design language v2 §8.1–8.4 (docs/03-design-architecture/iip_design_language_v2.md).
// Tokens only, see tailwind.config.ts `dls.*`, no literal hex/rgb/hsl.
"use client";

import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from "react";
import { cn } from "@/lib/utils";

type ButtonVariant = "primary" | "secondary" | "ghost" | "destructive";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  icon?: ReactNode;
}

const base =
  "inline-flex items-center justify-center gap-1.5 rounded-dls-control text-[13px] font-bold " +
  "px-4 py-[9px] transition-colors duration-dls-fast ease-dls " +
  "dls-focus-outline " +
  "disabled:cursor-not-allowed";

const variantClass: Record<ButtonVariant, string> = {
  // §8.1, stays violet when disabled ("not yet", never "broken").
  primary:
    "bg-dls-primary text-dls-text-inverse shadow-dls-primary hover:bg-dls-primary-hover active:bg-dls-primary-active disabled:opacity-60",
  // §8.2
  secondary:
    "bg-dls-surface border border-dls-border-input text-dls-text-primary hover:bg-dls-bg-page disabled:opacity-60",
  // §8.3, only for the low-emphasis half of a pair.
  ghost: "text-dls-text-secondary font-semibold hover:bg-dls-bg-page disabled:opacity-60",
  // §8.4, text-only, never a filled red button.
  destructive: "text-dls-danger-text font-bold px-0 py-0 hover:underline disabled:opacity-60",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = "secondary", icon, className, children, ...props },
  ref,
) {
  return (
    <button ref={ref} type={props.type ?? "button"} className={cn(base, variantClass[variant], className)} {...props}>
      {icon && <span className="h-3.5 w-3.5 shrink-0 [&>svg]:h-3.5 [&>svg]:w-3.5">{icon}</span>}
      {children}
    </button>
  );
});

export interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  "aria-label": string;
  active?: boolean;
}

// Icon-only is permitted for the rail, table row actions, and composer utilities, never for a
// consequential action (approve, reject, escalate, send, export, delete). See §7.
export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(function IconButton(
  { active, className, children, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      type={props.type ?? "button"}
      className={cn(
        "inline-flex h-10 w-10 items-center justify-center rounded-dls-control transition-colors duration-dls-fast ease-dls",
        "dls-focus-outline",
        active ? "bg-dls-primary-soft text-dls-primary" : "text-dls-text-secondary hover:bg-dls-bg-page",
        className,
      )}
      {...props}
    >
      {children}
    </button>
  );
});
