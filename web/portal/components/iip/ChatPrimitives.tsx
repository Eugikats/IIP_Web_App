// Design language v2 §8.17, chat surface. Shared by the Assistant landing (AIConversations)
// and the shell's CopilotPanel; keep every exported signature stable across both callers.
import { Sparkles, ArrowUp } from "lucide-react";
import { cn } from "@/lib/utils";
import type { ReactNode } from "react";

export function UserMessageBubble({ text }: { text: string }) {
  return (
    <div className="flex justify-end">
      <p className="max-w-[70%] whitespace-pre-wrap rounded-tl-dls-overlay rounded-tr-dls-overlay rounded-br-dls-tight rounded-bl-dls-overlay bg-dls-primary px-[18px] py-[11px] text-[14px] font-semibold leading-snug text-dls-text-inverse">
        {text}
      </p>
    </div>
  );
}

export function AIMessageBubble({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-start gap-3">
      <div className="flex h-[26px] w-[26px] shrink-0 items-center justify-center rounded-dls-chip bg-dls-primary-soft" aria-hidden="true">
        <Sparkles className="h-3.5 w-3.5 text-dls-primary" />
      </div>
      <div className="max-w-[78%] rounded-tl-dls-tight rounded-tr-dls-overlay rounded-br-dls-overlay rounded-bl-dls-overlay border border-dls-border-subtle bg-dls-surface px-5 py-4 text-[14px] leading-[1.65] text-dls-text-primary">
        <div className="space-y-3">{children}</div>
      </div>
    </div>
  );
}

export function Composer({ value, onChange, onSend, disabled, placeholder, ariaLabel, disclaimerClassName }: {
  value: string;
  onChange: (v: string) => void;
  onSend: () => void;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  // Escape hatch for the disclaimer line's background: CopilotPanel sits it on bg-dls-surface
  // (white), but AIConversations' hero sits it directly on bg-dls-bg-page, where the default
  // text-dls-text-muted measures 4.47:1, below WCAG AA 4.5:1 (gate-17).
  disclaimerClassName?: string;
}) {
  // Derive the send button's accessible name from the composer's own label so
  // multiple Composer instances on the same page (e.g. AIConversations + CopilotPanel
  // at desktop width) each expose a unique "Send to <surface>" label.  Playwright
  // strict mode and axe both fail when two buttons share the same name.
  const composerLabel = ariaLabel ?? "Message IIP Operations";
  const sendLabel = `Send to ${composerLabel.replace(/^Message /, "")}`;
  const hasValue = value.trim().length > 0;
  return (
    <div>
      <div className="flex items-end gap-2 rounded-dls-overlay border border-dls-border-input bg-dls-surface px-3.5 py-3 transition-colors duration-dls-fast ease-dls focus-within:border-dls-primary focus-within:ring-[3px] focus-within:ring-dls-primary/[0.12]">
        <textarea
          aria-label={composerLabel}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              onSend();
            }
          }}
          rows={1}
          // Must fit ONE line at the narrowest composer width (~400px: a 768px viewport, or a
          // 1280px one with the evidence panel open). The textarea is rows=1 with a fixed
          // min-height and does not grow, so a placeholder that wraps has its second line
          // clipped outright -- there is no scrollbar for placeholder text. The previous copy
          // ("... claims, documents, customers, policies, workflows") wrapped and lost its last
          // word at 768px. Breadth belongs in the suggestion chips above, which are real.
          placeholder={placeholder ?? "Ask about claims, documents or customers…"}
          className="max-h-40 min-h-11 flex-1 resize-none bg-transparent py-2 text-[14.5px] text-dls-text-primary outline-none placeholder:text-dls-text-muted"
        />
        <button
          type="button"
          onClick={onSend}
          disabled={disabled || !hasValue}
          aria-label={sendLabel}
          className={cn(
            "grid h-8 w-8 shrink-0 place-items-center rounded-dls-control transition-colors duration-dls-fast ease-dls dls-focus-outline",
            hasValue ? "bg-dls-primary text-dls-text-inverse" : "bg-dls-primary-idle text-dls-primary",
            disabled ? "cursor-not-allowed opacity-50" : undefined,
          )}
        >
          <ArrowUp className="h-4 w-4" aria-hidden="true" />
        </button>
      </div>
      <p className={cn("mt-2 text-center text-[12px]", disclaimerClassName ?? "text-dls-text-muted")}>
        IIP can make mistakes and does not make claim decisions. Verify before acting.
      </p>
    </div>
  );
}
