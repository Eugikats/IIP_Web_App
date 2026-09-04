import { ShieldCheck } from "lucide-react";

export default function GuardrailBanner({ text }: { text?: string }) {
  return (
    <div className="rounded-2xl border border-violet/22 bg-violet/6 px-4 py-2.5 flex items-center gap-2 text-[12px] text-violet">
      <ShieldCheck className="h-3.5 w-3.5 shrink-0" />
      <span className="text-ink-soft">
        {text ?? "AI prepares insurance work. Regulated decisions — settle, deny, repudiate, pay, bind, accuse fraud — remain with named human reviewers."}
      </span>
    </div>
  );
}
