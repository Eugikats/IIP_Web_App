import { ChevronDown, ChevronRight, Sparkles, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { cn } from "@/lib/utils";

export interface ContextStep {
  label: string;
  state: "done" | "active" | "pending";
}

interface Props {
  title?: string;
  steps: ContextStep[];
  sources?: string[];
  workflow?: string;
  className?: string;
}

export default function AIContextWindow({ title = "AI context", steps, sources = [], workflow, className }: Props) {
  const [open, setOpen] = useState(true);
  const active = steps.find((s) => s.state === "active");

  return (
    <div className={cn("rounded-2xl border border-[hsl(var(--violet)/0.20)] bg-[hsl(var(--violet)/0.04)] backdrop-blur-sm", className)}>
      <button
        onClick={() => setOpen((v) => !v)}
        className="w-full flex items-center gap-2 px-4 py-2.5 text-left"
      >
        <div className="h-6 w-6 rounded-lg gradient-primary grid place-items-center shrink-0">
          <Sparkles className="h-3 w-3 text-white" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="text-[10.5px] uppercase tracking-[0.14em] text-violet2">{title}</div>
          <div className="text-[12.5px] text-navy truncate">
            {active ? active.label : steps[steps.length - 1]?.label ?? "Idle"}
          </div>
        </div>
        {active && <span className="h-2 w-2 rounded-full bg-violet2 animate-pulse" />}
        {open ? <ChevronDown className="h-4 w-4 text-stone" /> : <ChevronRight className="h-4 w-4 text-stone" />}
      </button>

      {open && (
        <div className="px-4 pb-3 pt-1 space-y-2.5 animate-fade-in">
          <ol className="space-y-1.5">
            {steps.map((s, i) => (
              <li key={i} className="flex items-center gap-2 text-[12.5px]">
                <span
                  className={cn(
                    "h-1.5 w-1.5 rounded-full shrink-0",
                    s.state === "done" && "bg-success",
                    s.state === "active" && "bg-violet2 animate-pulse",
                    s.state === "pending" && "bg-cloud",
                  )}
                />
                <span className={cn(s.state === "done" ? "text-slate2" : s.state === "active" ? "text-navy font-medium" : "text-stone")}>
                  {s.label}
                </span>
              </li>
            ))}
          </ol>

          {sources.length > 0 && (
            <div>
              <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone mb-1.5">Using sources</div>
              <div className="flex flex-wrap gap-1.5">
                {sources.map((s) => (
                  <span key={s} className="inline-flex items-center gap-1 text-[11.5px] rounded-full bg-white/80 border border-cloud px-2 py-0.5 text-slate2">
                    <ShieldCheck className="h-3 w-3 text-info" /> {s}
                  </span>
                ))}
              </div>
            </div>
          )}

          {workflow && (
            <div className="text-[11.5px] text-stone">
              Preparing workflow step: <span className="text-violet2 font-medium">{workflow}</span>
            </div>
          )}

          <div className="text-[10.5px] text-stone/80 pt-1 border-t border-cloud/70">
            Safe operational transparency only · no prompts, secrets, or private reasoning are shown.
          </div>
        </div>
      )}
    </div>
  );
}
