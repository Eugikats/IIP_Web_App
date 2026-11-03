"use client";

import { useEffect, useMemo, useState } from "react";
import { Languages, Loader2, Mic, MessageSquareText, Save, Search, Volume2 } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { getAssistantLanguages, saveAssistantLanguages, type AssistantLanguageSettings as LanguageSettings } from "@/lib/assistantLanguages";

const tierTone = (tier: string) => tier === "Good" ? "success" : tier === "Moderate" ? "warning" : "neutral";

export default function AssistantLanguageSettings() {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const canConfigure = canUseLocalTestDoubles || parityMode || runtimeContext?.permissions.includes("ManageAgents") === true;
  const [state, setState] = useState<LanguageSettings | null>(null);
  const [draft, setDraft] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [query, setQuery] = useState("");

  async function load() {
    setLoading(true);
    setLoadError(null);
    const result = await getAssistantLanguages(roleDef.key);
    setLoading(false);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setState(result.value);
    setDraft(result.value.enabledCodes);
  }

  useEffect(() => { void load(); }, [roleDef.key]);

  const dirty = useMemo(() => JSON.stringify([...draft].sort()) !== JSON.stringify([...(state?.enabledCodes ?? [])].sort()), [draft, state?.enabledCodes]);
  const enabled = useMemo(() => new Set(draft), [draft]);

  function toggle(code: string) {
    setDraft((current) => current.includes(code) ? current.filter((item) => item !== code) : [...current, code]);
  }

  async function save(reason: string) {
    setSaving(true);
    setSaveError(null);
    const result = await saveAssistantLanguages(roleDef.key, draft, reason);
    setSaving(false);
    if (!result.ok) {
      setSaveError(result.error);
      return;
    }
    setDialogOpen(false);
    await load();
  }

  if (!canConfigure) return <NoPermissionStateSurface permission="ManageAgents" />;

  const languages = state?.languages ?? [];
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const grouped = ["Good", "Moderate", "Basic"].map((tier) => ({
    tier,
    items: languages.filter((language) =>
      language.translationQuality === tier
      && (!normalizedQuery || language.name.toLocaleLowerCase().includes(normalizedQuery) || language.code.toLocaleLowerCase().includes(normalizedQuery))),
  }));

  return (
    <>
      <SectionHeader eyebrow="Settings · Customer experience" title="Customer languages" sub="Choose the text languages this workspace can use with its connected translation provider." />
      {loading && !state ? <GlassCard><LoadingSkeleton rows={5} /></GlassCard> : null}
      {loadError && !state ? <GlassCard><ErrorStateSurface message={loadError} onRetry={() => void load()} /></GlassCard> : null}
      {state ? (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex items-start gap-3">
                <div className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Languages className="h-4 w-4" aria-hidden="true" /></div>
                <div>
                  <h2 className="text-[14px] font-bold text-dls-text-primary">Translation service</h2>
                  <p className="mt-1 text-[12.5px] leading-5 text-dls-text-secondary">Language names and quality tiers follow Sunbird AI&apos;s published Sunflower text model card. This is a preview model; coverage varies by language, and a tier does not certify insurance terminology or customer-ready quality.</p>
                  <p className="mt-2 text-[11.5px] text-dls-text-muted">Settings source: {state.configurationSource}. English is always available. Save only languages tested with this workspace&apos;s connected model.</p>
                </div>
              </div>
              <Badge tone={state.translationProviderStatus === "Healthy" ? "success" : state.translationProviderStatus === "Failing" ? "error" : "neutral"}>
                Translation {state.translationProviderStatus === "Healthy" ? "connected" : state.translationProviderStatus === "Failing" ? "unavailable" : "Not configured"}
              </Badge>
            </div>
            {state.translationProviderStatus !== "Healthy" ? <p className="mt-3 text-[11.5px] leading-5 text-dls-text-secondary">Connect and validate a translation provider before relying on non-English replies.</p> : null}
            {!state.allowUnreviewedTranslations ? <p className="mt-3 rounded-dls-control bg-dls-warning-tint px-3 py-2 text-[11.5px] leading-5 text-dls-warning-text">Machine-translated replies are disabled by deployment policy. Selected languages can be tested, but customer replies remain in English until that policy is enabled.</p> : null}
          </GlassCard>

          <GlassCard>
            <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
              <div className="min-w-0 flex-1">
                <h2 className="text-[14px] font-bold text-dls-text-primary">Text translation languages</h2>
                <p className="mt-1 text-[12px] text-dls-text-secondary">Select the languages customers may request. Low-resource output should be reviewed by a speaker before use.</p>
              </div>
              <div className="flex w-full flex-wrap items-center justify-between gap-3 sm:w-auto sm:justify-end">
                <label className="relative min-w-[190px] flex-1 sm:flex-none">
                  <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-dls-text-muted" aria-hidden="true" />
                  <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search languages" aria-label="Search languages" className="dls-focus-outline h-9 w-full rounded-dls-control border border-dls-border-input bg-dls-surface pl-9 pr-3 text-[11.5px] text-dls-text-primary placeholder:text-dls-text-muted sm:w-52" />
                </label>
                <span className="shrink-0 text-[11px] text-dls-text-muted">{draft.length} selected</span>
              </div>
            </div>
            {languages.length === 0 ? <EmptyStateSurface title="No language catalog available" message="The language catalog could not be loaded for this workspace." /> : (
              grouped.every(({ items }) => items.length === 0) ? <EmptyStateSurface title="No matching languages" message="Try a different language name or code." /> : (
              <div className="space-y-5">
                {grouped.map(({ tier, items }) => items.length === 0 ? null : (
                  <section key={tier} aria-labelledby={`language-tier-${tier.toLowerCase()}`}>
                    <div className="mb-2 flex items-center gap-2">
                      <h3 id={`language-tier-${tier.toLowerCase()}`} className="text-[11px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">{tier} model coverage</h3>
                      <Badge tone={tierTone(tier) as "success" | "warning" | "neutral"}>{items.length}</Badge>
                    </div>
                    <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                      {items.map((language) => (
                        <label key={language.code} className={`dls-focus-outline flex cursor-pointer items-start gap-3 rounded-dls-card border p-3 transition-colors ${enabled.has(language.code) ? "border-dls-primary-border bg-dls-primary-soft-alt" : "border-dls-border-subtle hover:bg-dls-bg-page"}`}>
                          <input type="checkbox" checked={enabled.has(language.code)} onChange={() => toggle(language.code)} className="mt-0.5 h-4 w-4 accent-dls-primary" aria-label={`Enable ${language.name}`} />
                          <span className="min-w-0 flex-1">
                            <span className="flex flex-wrap items-center gap-2 text-[12.5px] font-semibold text-dls-text-primary">{language.name}<Badge tone={tierTone(language.translationQuality) as "success" | "warning" | "neutral"}>{language.translationQuality}</Badge></span>
                            <span className="mt-1 block text-[10.5px] text-dls-text-muted">{language.code}</span>
                            <span className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[10.5px] text-dls-text-muted">
                              <span className="inline-flex items-center gap-1"><Mic className="h-3 w-3" aria-hidden="true" /> Voice input: {language.voiceInput}</span>
                              <span className="inline-flex items-center gap-1"><Volume2 className="h-3 w-3" aria-hidden="true" /> Voice output: {language.voiceOutput}</span>
                            </span>
                          </span>
                        </label>
                      ))}
                    </div>
                  </section>
                ))}
              </div>
              )
            )}
            {saveError ? <p role="alert" className="mt-4 rounded-dls-control bg-dls-danger-tint px-3 py-2 text-[12px] text-dls-danger-text">{saveError}</p> : null}
            <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-dls-border-subtle pt-4">
              <p className="max-w-2xl text-[11px] leading-4 text-dls-text-muted">Changing the selection affects new assistant replies. Existing customer language preferences are retained, but a disabled language falls back to English.</p>
              <Button onClick={() => setDialogOpen(true)} disabled={!dirty || saving} icon={saving ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Save aria-hidden="true" />}>Review changes</Button>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="flex items-start gap-3">
              <div className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><MessageSquareText className="h-4 w-4" aria-hidden="true" /></div>
              <div>
                <h2 className="text-[14px] font-bold text-dls-text-primary">Capability boundaries</h2>
                <p className="mt-1 text-[12px] leading-5 text-dls-text-secondary">Text translation and voice recognition/synthesis have different model coverage. IIP currently wires voice input and output for Luganda and Kiswahili only; the other voice capabilities are shown as Not configured. Language selection does not change claim decision authority or bypass human review.</p>
              </div>
            </div>
          </GlassCard>
        </div>
      ) : null}
      <GovernedActionDialog
        open={dialogOpen}
        title="Change customer languages"
        target={`${draft.length} enabled non-English languages`}
        impact="This changes the customer assistant language options for this workspace. It does not enable a provider or approve translation quality."
        auditNotice="The change is recorded in the workspace audit trail. Include why you are enabling or removing these languages."
        confirmLabel={saving ? "Saving…" : "Save languages"}
        onClose={() => setDialogOpen(false)}
        onConfirm={save}
      />
    </>
  );
}
