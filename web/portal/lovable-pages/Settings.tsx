"use client";

import { LogOut, User, Building2, Loader2 } from "lucide-react";
import { useState, type ReactNode } from "react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { signOut, getEntraApiAccessToken } from "@/lib/entraAuth";
import {
  apiBaseUrl,
  canUseLocalTestDoubles,
  getActiveWorkspaceOverride,
  scopedHeaders,
  setActiveWorkspaceOverride,
} from "@/lib/config";

// WO-13: replaces the previous 4-line redirect-to-/app/admin stub with a real settings
// screen. Profile identity (name, role, tenant) is authoritative from Microsoft Entra ID /
// the membership store via GET /api/v1/me, IIP has no self-service identity editor for
// those fields, so they render read-only rather than as fields that can't actually save (A4).
//
// Workspace switching sends the chosen workspace as a request header
// (RuntimeClaimTypes.RequestedWorkspaceHeaderName). The backend treats it only as a hint and
// re-validates it against the user's real membership (GetEffectiveMembershipAsync) before
// honoring it, a switch to a workspace the user doesn't belong to fails closed with 403. This
// is server-side scope re-resolution, not client-side scope substitution (I5).
export default function Settings() {
  const { runtimeContext, loading, error, parityMode, refresh } = useRuntimeContext();
  const [signingOut, setSigningOut] = useState(false);
  const [switchingTo, setSwitchingTo] = useState<string | null>(null);
  const [switchError, setSwitchError] = useState<string | null>(null);

  async function handleSignOut() {
    setSigningOut(true);
    try {
      await signOut();
    } finally {
      setSigningOut(false);
    }
  }

  async function handleSwitchWorkspace(workspaceId: string) {
    if (!runtimeContext || parityMode || workspaceId === runtimeContext.workspaceId || switchingTo) {
      return;
    }

    const previousOverride = getActiveWorkspaceOverride();
    setSwitchingTo(workspaceId);
    setSwitchError(null);
    setActiveWorkspaceOverride(workspaceId);

    try {
      const token = await getEntraApiAccessToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/me`, {
        headers: {
          ...scopedHeaders(canUseLocalTestDoubles ? "Admin" : runtimeContext.role, ""),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!response.ok) {
        throw new Error(
          response.status === 403
            ? "You do not have access to that workspace."
            : `Workspace switch failed.`
        );
      }

      // A full reload guarantees every screen re-fetches with the newly re-resolved scope,
      // rather than leaving already-loaded data from the previous workspace on screen.
      window.location.reload();
    } catch (err) {
      setActiveWorkspaceOverride(previousOverride);
      setSwitchError(err instanceof Error ? err.message : "Workspace switch failed.");
      setSwitchingTo(null);
      void refresh();
    }
  }

  return (
    <>
      <SectionHeader eyebrow="Account" title="Settings" sub="Your profile, workspace, and session, resolved from your signed-in identity." />

      {loading && !runtimeContext ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : !runtimeContext ? (
        <GlassCard><ErrorStateSurface message={error?.message ?? "Your account could not be resolved."} /></GlassCard>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          <GlassCard>
            <div className="mb-4 flex items-center gap-2">
              <div className="grid h-9 w-9 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><User className="h-4 w-4" aria-hidden="true" /></div>
              <div className="text-[14px] font-bold text-dls-text-primary">Profile</div>
            </div>
            <dl className="space-y-3 text-[13px]">
              <Row label="Display name" value={runtimeContext.displayName} />
              <Row label="User ID" value={runtimeContext.userId || "-"} />
              <Row label="Role" value={<Badge tone="neutral">{runtimeContext.roleLabel}</Badge>} />
              <Row label="Identity source" value={runtimeContext.source} />
            </dl>
            <p className="mt-4 text-[11.5px] leading-relaxed text-dls-text-muted">
              {parityMode || canUseLocalTestDoubles
                ? "This is a local development identity, supplied by request headers rather than a real sign-in session."
                : "Your name and role are managed by your organization's identity provider and workspace administrator. Contact your administrator to change them."}
            </p>
          </GlassCard>

          <GlassCard>
            <div className="mb-4 flex items-center gap-2">
              <div className="grid h-9 w-9 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Building2 className="h-4 w-4" aria-hidden="true" /></div>
              <div className="text-[14px] font-bold text-dls-text-primary">Workspace</div>
            </div>
            <dl className="mb-4 space-y-3 text-[13px]">
              <Row label="Tenant ID" value={runtimeContext.tenantId || "-"} />
            </dl>
            {switchError && (
              <div className="mb-3 rounded-dls-control bg-dls-danger-tint px-3 py-2 text-[11.5px] text-dls-danger-text">
                {switchError}
              </div>
            )}
            <div className="space-y-2">
              {runtimeContext.availableWorkspaces.map((workspace) => {
                const isCurrent = workspace.workspaceId === runtimeContext.workspaceId;
                const isSwitching = switchingTo === workspace.workspaceId;
                return (
                  <button
                    key={workspace.workspaceId}
                    type="button"
                    onClick={() => handleSwitchWorkspace(workspace.workspaceId)}
                    disabled={isCurrent || !!switchingTo || parityMode}
                    className={
                      "dls-focus-outline flex w-full items-center justify-between rounded-dls-card border px-3 py-2.5 text-left transition-colors duration-dls-fast ease-dls disabled:cursor-default " +
                      (isCurrent
                        ? "border-dls-primary-border bg-dls-primary-soft-alt"
                        : "border-transparent hover:border-dls-border-subtle hover:bg-dls-bg-page")
                    }
                  >
                    <div className="min-w-0">
                      <div className="truncate text-[12.5px] font-semibold text-dls-text-primary">{workspace.name}</div>
                      <div className="truncate text-[10.5px] text-dls-text-muted">{workspace.workspaceId}</div>
                    </div>
                    {isCurrent ? (
                      <Badge tone="info">Current</Badge>
                    ) : isSwitching ? (
                      <span className="inline-flex items-center gap-1.5 text-[11px] text-dls-text-muted"><Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" /> Switching…</span>
                    ) : (
                      <span className="text-[11px] font-semibold text-dls-primary">Switch to</span>
                    )}
                  </button>
                );
              })}
            </div>
            <p className="mt-4 text-[11.5px] leading-relaxed text-dls-text-muted">
              {parityMode
                ? "Workspace switching is disabled in visual parity preview."
                : "Switching re-resolves your permissions and data on the server for the selected workspace."}
            </p>
          </GlassCard>

          <GlassCard className="lg:col-span-2">
            <div className="mb-3 flex items-center gap-2">
              <div className="grid h-9 w-9 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><LogOut className="h-4 w-4" aria-hidden="true" /></div>
              <div className="text-[14px] font-bold text-dls-text-primary">Session</div>
            </div>
            <div className="mb-3">
              <GuardrailBanner text="Signing out ends your session on this device. It does not change your permissions or workspace assignment." />
            </div>
            <Button
              variant="secondary"
              onClick={handleSignOut}
              disabled={signingOut}
              icon={<LogOut aria-hidden="true" />}
            >
              {signingOut ? "Signing out…" : "Sign out"}
            </Button>
          </GlassCard>
        </div>
      )}
    </>
  );
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-dls-text-muted">{label}</dt>
      <dd className="max-w-[60%] truncate text-right font-semibold text-dls-text-primary">{value}</dd>
    </div>
  );
}
