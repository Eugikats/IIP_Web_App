"use client";

import { useId, useState } from "react";
import { AlertTriangle, ArrowRight, CheckCircle2, Languages, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Select, Textarea, Field as UIField } from "@/components/ui/Input";
import { TRANSLATION_FAILURE_WORDS, tryTranslation, type TranslationTryResult } from "@/lib/connections";

const LANGUAGES = [
  { iso: "en", name: "English" },
  { iso: "lg", name: "Luganda" },
  { iso: "sw", name: "Kiswahili" },
];

const MAX_LENGTH = 600;

// Runs a piece of text through the connected engine with the same checks and fallback a customer's
// message gets, so a person can read real output before customers depend on it. Stores nothing.
export function TranslationTryPanel({ role }: { role: string }) {
  const textId = useId();
  const fromId = useId();
  const toId = useId();
  const [text, setText] = useState("Your claim has been received. Please send photos of the damage and a copy of the police report.");
  const [from, setFrom] = useState("en");
  const [to, setTo] = useState("lg");
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<TranslationTryResult | null>(null);

  const trimmed = text.trim();
  const canRun = !running && trimmed.length > 0 && trimmed.length <= MAX_LENGTH && from !== to;

  async function run() {
    setRunning(true);
    setError(null);
    setResult(null);
    const outcome = await tryTranslation(role, { text: trimmed, from, to });
    setRunning(false);
    if (!outcome.ok) {
      setError(outcome.error);
      return;
    }
    setResult(outcome.value);
  }

  return (
    <div className="mt-5 border-t border-dls-border-subtle pt-4">
      <div className="mb-1 flex items-center gap-2">
        <Languages className="h-4 w-4 text-dls-text-muted" aria-hidden="true" />
        <div className="text-[14px] font-bold text-dls-text-primary">Try a translation</div>
      </div>
      <p className="mb-3 text-[12px] text-dls-text-secondary">
        Runs through the connected engine with the same checks a customer message gets. Nothing is sent or stored.
      </p>

      <div className="space-y-3">
        <UIField label="Text" hint={`${trimmed.length} of ${MAX_LENGTH} characters`} htmlFor={textId}>
          <Textarea id={textId} rows={3} value={text} onChange={(event) => setText(event.target.value)} maxLength={MAX_LENGTH} />
        </UIField>

        <div className="flex flex-wrap items-end gap-3">
          <div className="min-w-[140px] flex-1">
            <UIField label="From" htmlFor={fromId}>
              <Select id={fromId} value={from} onChange={(event) => setFrom(event.target.value)}>
                {LANGUAGES.map((language) => <option key={language.iso} value={language.iso}>{language.name}</option>)}
              </Select>
            </UIField>
          </div>
          <ArrowRight className="mb-2.5 h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden="true" />
          <div className="min-w-[140px] flex-1">
            <UIField label="To" htmlFor={toId}>
              <Select id={toId} value={to} onChange={(event) => setTo(event.target.value)}>
                {LANGUAGES.map((language) => <option key={language.iso} value={language.iso}>{language.name}</option>)}
              </Select>
            </UIField>
          </div>
          <Button
            variant="primary"
            onClick={run}
            disabled={!canRun}
            icon={running ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
          >
            {running ? "Translating" : "Translate"}
          </Button>
        </div>
        {from === to && <div className="text-[12px] text-dls-text-muted">Choose two different languages.</div>}
      </div>

      {error && (
        <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {error}
        </div>
      )}

      {result && result.translated && (
        <div className="mt-4 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3">
          <div className="mb-1 flex items-center gap-1.5 text-[11px] font-semibold text-dls-success-text">
            <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> Passed the translation checks{result.fromCache ? " (from cache)" : ""}
          </div>
          <div className="whitespace-pre-wrap text-[13px] text-dls-text-primary">{result.text}</div>
          <div className="mt-2 text-[11px] text-dls-text-muted">
            Machine translated. Have a native speaker confirm it says exactly what the original says before relying on it.
          </div>
        </div>
      )}

      {result && !result.translated && (
        <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-warning-tint p-3 text-[12px] text-dls-warning-text">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            Rejected, a customer would get the original text instead.{" "}
            {result.failure ? TRANSLATION_FAILURE_WORDS[result.failure] ?? "" : ""}
          </span>
        </div>
      )}
    </div>
  );
}
