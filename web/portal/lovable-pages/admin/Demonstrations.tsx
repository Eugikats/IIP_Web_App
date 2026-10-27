"use client";

import { useCallback, useEffect, useState } from "react";
import { Landmark, ShieldCheck, Umbrella } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";
import { ErrorStateSurface, LoadingSkeleton, NotConfiguredStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { setActiveOrganisationOverride } from "@/lib/config";
import { activateDemoScenario, listDemoScenarios, type DemoScenario } from "@/lib/demoScenarios";
import { useRuntimeContext } from "@/lib/runtimeContext";

const ICONS: Record<string, typeof Landmark> = {
  "mavuno-bank": Landmark,
  "nile-life": ShieldCheck,
  "nile-general": Umbrella,
};

const FOCUS_LABEL: Record<DemoScenario["lineFocus"], string> = {
  Both: "Life and general",
  Life: "Life",
  General: "General",
};

export default function Demonstrations() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const { runtimeContext } = useRuntimeContext();
  const activeOrganisation = runtimeContext?.tenantId ?? null;

  const [scenarios, setScenarios] = useState<DemoScenario[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [notAvailable, setNotAvailable] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    setNotAvailable(false);
    const result = await listDemoScenarios(role);
    if (!result.ok) {
      setScenarios(null);
      if (result.notAvailable) setNotAvailable(true);
      else setLoadError(result.error);
      return;
    }
    setScenarios(result.value);
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function open(scenario: DemoScenario) {
    setBusy(scenario.id);
    setActionError(null);
    const result = await activateDemoScenario(role, scenario.id);
    if (!result.ok) {
      setBusy(null);
      setActionError(result.error);
      return;
    }
    // Acting in that organisation from now on; a full load makes every screen read it afresh.
    setActiveOrganisationOverride(result.value.organisationId, result.value.workspaceId);
    window.location.assign("/app");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Demonstrations"
        title="Demonstrations"
        sub="Three ready-made worlds with invented people, each in an organisation of its own. Choosing one acts in that organisation. Nothing in one is copied into another, and none contains a real customer."
      />

      {!scenarios && !loadError && !notAvailable && (
        <GlassCard>
          <LoadingSkeleton rows={3} />
        </GlassCard>
      )}

      {notAvailable && (
        <GlassCard>
          <NotConfiguredStateSurface what="Demonstrations are not available in this environment. They exist only where demonstration data is allowed." />
        </GlassCard>
      )}

      {loadError && (
        <GlassCard>
          <ErrorStateSurface title="The demonstrations could not be loaded" message={loadError} onRetry={() => void load()} />
        </GlassCard>
      )}

      {actionError && (
        <div className="mb-3">
          <GlassCard>
            <ErrorStateSurface title="This demonstration could not be opened" message={actionError} />
          </GlassCard>
        </div>
      )}

      {scenarios && (
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
          {scenarios.map((scenario) => {
            const Icon = ICONS[scenario.id] ?? Landmark;
            const active = scenario.ready && scenario.organisationId === activeOrganisation;
            return (
              <GlassCard key={scenario.id} className="h-full">
                <div className="flex h-full flex-col gap-3" data-testid={`scenario-${scenario.id}`}>
                  <div className="flex items-start gap-3">
                    <div className="grid h-10 w-10 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary">
                      <Icon className="h-4 w-4" aria-hidden="true" />
                    </div>
                    <div>
                      <div className="text-[14px] font-bold text-dls-text-primary">{scenario.title}</div>
                      <div className="mt-1 flex flex-wrap items-center gap-2">
                        <StatusPill
                          tone={active ? "success" : "neutral"}
                          label={active ? "Active now" : scenario.ready ? "Set up" : "Not set up"}
                        />
                        <span className="text-[12px] text-dls-text-muted">{FOCUS_LABEL[scenario.lineFocus]}</span>
                      </div>
                    </div>
                  </div>
                  <p className="text-[13px] leading-relaxed text-dls-text-secondary">{scenario.summary}</p>
                  <p className="text-[12.5px] text-dls-text-muted">{scenario.emphasis}</p>
                  {scenario.partnerInsurers.length > 0 && (
                    <p className="text-[12.5px] text-dls-text-muted">Sells for: {scenario.partnerInsurers.join(", ")}</p>
                  )}
                  <div className="mt-auto pt-1">
                    <Button
                      variant={active ? "secondary" : "primary"}
                      disabled={busy !== null}
                      onClick={() => void open(scenario)}
                    >
                      {busy === scenario.id
                        ? "Setting up…"
                        : active
                          ? "Refresh its data"
                          : scenario.ready
                            ? "Open this demonstration"
                            : "Set it up and open"}
                    </Button>
                  </div>
                </div>
              </GlassCard>
            );
          })}
        </div>
      )}
    </>
  );
}
