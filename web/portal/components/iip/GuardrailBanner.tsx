// Design language v2, shared by 19 screens; a foundation-level dependency, migrated once here.
import { ShieldCheck } from "lucide-react";

export default function GuardrailBanner({ text }: { text?: string }) {
  return (
    <div className="flex items-center gap-2 rounded-dls-card border border-dls-primary-border bg-dls-primary-soft-alt px-4 py-2.5 text-[12px] text-dls-text-secondary">
      <ShieldCheck className="h-3.5 w-3.5 shrink-0 text-dls-primary" aria-hidden="true" />
      <span>
        {text ?? "AI prepares insurance work. Regulated decisions, settle, deny, repudiate, pay, bind, accuse fraud, remain with named human reviewers."}
      </span>
    </div>
  );
}
