"use client";

/** The three bouncing dots shown while an answer is being prepared. */
export default function TypingDots({ label = "IIP is preparing an answer" }: { label?: string }) {
  return (
    <div
      className="inline-flex items-center gap-1 rounded-2xl rounded-tl-sm border border-slate-100 bg-white px-3.5 py-3 shadow-[0_1px_2px_rgba(15,23,42,0.04)]"
      role="status"
      aria-live="polite"
      aria-label={label}
    >
      <span className="cip-cc-typing-dot" />
      <span className="cip-cc-typing-dot" />
      <span className="cip-cc-typing-dot" />
    </div>
  );
}
