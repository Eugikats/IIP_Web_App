"use client";

import { LogOut, User, Building2, Loader2 } from "lucide-react";
import { useState, type ReactNode } from "react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
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
// the membership store via GET /api/v1/me — IIP has no self-service identity editor for
// those fields, so they render read-only rather than as fields that can't actually save (A4).
//
// Workspace switching sends the chosen workspace as a request header
// (RuntimeClaimTypes.RequestedWorkspaceHeaderName). The backend treats it only as a hint and
// re-validates it against the user's real membership (GetEffectiveMembershipAsync) before
// honoring it — a switch to a workspace the user doesn't belong to fails closed with 403. This
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
            : `Workspace switch failed (HTTP ${response.status}).`
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
      <SectionHeader eyebrow="Account" title="Settings" sub="Your profile, workspace, and session — resolved from your signed-in identity." />

      {loading && !runtimeContext ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading your account…</div></GlassCard>
      ) : !runtimeContext ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">{error?.message ?? "Your account could not be resolved."}</div></GlassCard>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          <GlassCard>
            <div className="flex items-center gap-2 mb-4">
              <div className="h-9 w-9 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><User className="h-4 w-4" /></div>
              <div className="text-[14px] font-semibold text-navy">Profile</div>
            </div>
            <dl className="space-y-3 text-[13px]">
              <Row label="Display name" value={runtimeContext.displayName} />
              <Row label="User ID" value={runtimeContext.userId || "—"} />
              <Row label="Role" value={<Badge tone="neutral">{runtimeContext.roleLabel}</Badge>} />
              <Row label="Identity source" value={runtimeContext.source} />
            </dl>
            <p className="mt-4 text-[11.5px] text-stone leading-relaxed">
              {parityMode || canUseLocalTestDoubles
                ? "This is a local development identity, supplied by request headers rather than a real sign-in session."
                : "Your name and role are managed by your organization's identity provider and workspace administrator. Contact your administrator to change them."}
            </p>
          </GlassCard>

          <GlassCard>
            <div className="flex items-center gap-2 mb-4">
              <div className="h-9 w-9 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><Building2 className="h-4 w-4" /></div>
              <div className="text-[14px] font-semibold text-navy">Workspace</div>
            </div>
            <dl className="space-y-3 text-[13px] mb-4">
              <Row label="Tenant ID" value={runtimeContext.tenantId || "—"} />
            </dl>
            {switchError && (
              <div className="mb-3 rounded-xl border border-[hsl(var(--error)/0.25)] bg-[hsl(var(--error)/0.06)] px-3 py-2 text-[11.5px] text-[hsl(var(--error))]">
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
                      "flex w-full items-center justify-between rounded-xl border px-3 py-2.5 text-left transition-colors disabled:cursor-default " +
                      (isCurrent
                        ? "border-info/30 bg-[hsl(var(--info)/0.06)]"
                        : "border-cloud bg-white/60 hover:bg-white disabled:hover:bg-white/60")
                    }
                  >
                    <div className="min-w-0">
                      <div className="text-[12.5px] font-medium text-navy truncate">{workspace.name}</div>
                      <div className="text-[10.5px] text-stone truncate">{workspace.workspaceId}</div>
                    </div>
                    {isCurrent ? (
                      <Badge tone="info">Current</Badge>
                    ) : isSwitching ? (
                      <span className="inline-flex items-center gap-1.5 text-[11px] text-stone"><Loader2 className="h-3 w-3 animate-spin" /> Switching…</span>
                    ) : (
                      <span className="text-[11px] text-info">Switch to</span>
                    )}
                  </button>
                );
              })}
            </div>
            <p className="mt-4 text-[11.5px] text-stone leading-relaxed">
              {parityMode
                ? "Workspace switching is disabled in visual parity preview."
                : "Switching re-resolves your permissions and data on the server for the selected workspace."}
            </p>
          </GlassCard>

          <GlassCard className="lg:col-span-2">
            <div className="flex items-center gap-2 mb-3">
              <div className="h-9 w-9 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><LogOut className="h-4 w-4" /></div>
              <div className="text-[14px] font-semibold text-navy">Session</div>
            </div>
            <div className="mb-3">
              <GuardrailBanner text="Signing out ends your session on this device. It does not change your permissions or workspace assignment." />
            </div>
            <button
              type="button"
              onClick={handleSignOut}
              disabled={signingOut}
              className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-2.5 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
            >
              <LogOut className="h-4 w-4" /> {signingOut ? "Signing out…" : "Sign out"}
            </button>
          </GlassCard>
        </div>
      )}
    </>
  );
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-stone">{label}</dt>
      <dd className="text-navy font-medium text-right truncate max-w-[60%]">{value}</dd>
    </div>
  );
}
