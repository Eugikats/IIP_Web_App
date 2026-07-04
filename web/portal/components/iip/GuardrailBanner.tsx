import { ShieldCheck } from "lucide-react";

export default function GuardrailBanner({ text }: { text?: string }) {
  return (
    <div className="rounded-2xl border border-[hsl(var(--violet)/0.22)] bg-[hsl(var(--violet)/0.06)] px-4 py-2.5 flex items-center gap-2 text-[12px] text-violet2">
      <ShieldCheck className="h-3.5 w-3.5 shrink-0" />
      <span className="text-slate2">
        {text ?? "AI prepares insurance work. Regulated decisions — settle, deny, repudiate, pay, bind, accuse fraud — remain with named human reviewers."}
      </span>
    </div>
  );
}
