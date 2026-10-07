// Ported from the CIP web app (apps/web/components/dashboard/ui) so IIP and CIP share one look.
// Only the import of `cn` and a trend check that refuses to draw a flat line were changed.
import type { ComponentPropsWithoutRef, ElementType, ReactNode } from "react";
import { cn } from "@/lib/utils";

type DashboardCardProps<T extends ElementType = "section"> = {
  as?: T;
  children: ReactNode;
  className?: string;
  interactive?: boolean;
  padding?: "none" | "sm" | "md" | "lg";
  tone?: "default" | "subtle";
} & Omit<ComponentPropsWithoutRef<T>, "as" | "children" | "className">;

const paddingClasses = {
  none: "",
  sm: "p-3",
  md: "p-4",
  lg: "p-5",
};

const toneClasses = {
  default: "border-slate-200/80 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.04)]",
  subtle: "border-slate-200/70 bg-white/95 shadow-[0_1px_2px_rgba(15,23,42,0.03)]",
};

export default function DashboardCard<T extends ElementType = "section">({
  as,
  children,
  className,
  interactive = false,
  padding = "md",
  tone = "default",
  ...props
}: DashboardCardProps<T>) {
  const Component = as ?? "section";

  return (
    <Component
      className={cn(
        "rounded-2xl border text-slate-950",
        toneClasses[tone],
        paddingClasses[padding],
        interactive && "transition-all hover:-translate-y-0.5 hover:border-violet-200 hover:shadow-[0_8px_24px_rgba(15,23,42,0.08)]",
        className
      )}
      {...props}
    >
      {children}
    </Component>
  );
}
