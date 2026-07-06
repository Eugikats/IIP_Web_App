import { Activity, CheckCircle2, RefreshCw, Zap } from "lucide-react";

export default function StatusBar() {
  return (
    <div className="h-8 shrink-0 border-t border-cloud/70 bg-white/60 backdrop-blur-xl px-4 flex items-center gap-5 text-[11px] text-stone">
      <span className="inline-flex items-center gap-1.5"><span className="h-1.5 w-1.5 rounded-full bg-success animate-pulse-soft" /> Connected · Azure EA-North</span>
      <span className="inline-flex items-center gap-1.5"><RefreshCw className="h-3 w-3" /> Last sync 12s ago</span>
      <span className="inline-flex items-center gap-1.5"><Activity className="h-3 w-3" /> 6 active workflows</span>
      <span className="inline-flex items-center gap-1.5"><Zap className="h-3 w-3 text-violet2" /> Queue 14</span>
      <span className="ml-auto inline-flex items-center gap-1.5"><CheckCircle2 className="h-3 w-3 text-success" /> Providers healthy · GPT-4o · Azure OCR · WhatsApp Cloud</span>
    </div>
  );
}
