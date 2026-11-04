"use client";

import { useEffect, useState, type CSSProperties } from "react";
import iipLogo from "@/assets/iip-logo";
import { apiBaseUrl } from "@/lib/config";
import { clientPortalHeaders } from "@/lib/clientPortal";

/** What a customer's pages say about the insurer they belong to. Nothing here is private to the insurer. */
export type PortalBranding = {
  slug: string | null;
  displayName: string | null;
  accentColour: string | null;
  logoUrl: string | null;
  supportPhone: string | null;
  supportEmail: string | null;
};

let pending: Promise<PortalBranding | null> | null = null;

/** Asked for once per page load. A failure is not an error the customer needs to see: the pages simply keep the platform's own look. */
function loadBranding(): Promise<PortalBranding | null> {
  if (!pending) {
    pending = fetch(`${apiBaseUrl}/api/v1/client-portal/branding`, { headers: clientPortalHeaders(), cache: "no-store" })
      .then((response) => (response.ok ? (response.json() as Promise<PortalBranding>) : null))
      .catch(() => null);
  }
  return pending;
}

export function usePortalBranding(): PortalBranding | null {
  const [branding, setBranding] = useState<PortalBranding | null>(null);
  useEffect(() => {
    let cancelled = false;
    void loadBranding().then((found) => {
      if (!cancelled) setBranding(found);
    });
    return () => {
      cancelled = true;
    };
  }, []);
  return branding;
}

function toRgb(hex: string): [number, number, number] | null {
  return /^#[0-9a-fA-F]{6}$/.test(hex) ? [1, 3, 5].map((start) => parseInt(hex.slice(start, start + 2), 16)) as [number, number, number] : null;
}

function darker([r, g, b]: [number, number, number], by: number): string {
  return [r, g, b].map((channel) => Math.round(channel * (1 - by))).join(" ");
}

/** The insurer's main-button colour as the page's primary colour. Only a valid colour is used; the server has already checked it reads with white text. */
export function brandStyle(branding: PortalBranding | null): CSSProperties | undefined {
  const rgb = branding?.accentColour ? toRgb(branding.accentColour) : null;
  if (!rgb) return undefined;
  return {
    "--color-primary": rgb.join(" "),
    "--color-primary-hover": darker(rgb, 0.12),
    "--color-primary-active": darker(rgb, 0.22),
  } as CSSProperties;
}

/** The insurer's logo when it has one, otherwise the platform's mark. */
export function PortalBrandMark({ branding, large = false }: { branding: PortalBranding | null; large?: boolean }) {
  const size = large ? "h-8 w-8" : "h-7 w-7";
  const frame = large ? "h-10 w-10" : "h-9 w-9";
  return (
    <span className={`grid ${frame} place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface`}>
      {branding?.logoUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={branding.logoUrl} alt={branding.displayName ?? "Insurer logo"} className={`${size} object-contain`} referrerPolicy="no-referrer" />
      ) : (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={iipLogo.url} alt="IIP" className={`${size} object-contain`} />
      )}
    </span>
  );
}

export function brandName(branding: PortalBranding | null): string {
  return branding?.displayName ?? "Insurance Intelligence Platform";
}

/** Who to call or write to, when the insurer has said. */
export function SupportLine({ branding }: { branding: PortalBranding | null }) {
  if (!branding || (!branding.supportPhone && !branding.supportEmail)) return null;
  return (
    <p className="mt-6 text-center text-xs text-dls-text-muted" data-testid="portal-support-line">
      Need help? {branding.supportPhone ? <>Call <span className="font-semibold">{branding.supportPhone}</span></> : null}
      {branding.supportPhone && branding.supportEmail ? " or " : null}
      {branding.supportEmail ? <>write to <a className="font-semibold underline" href={`mailto:${branding.supportEmail}`}>{branding.supportEmail}</a></> : null}.
    </p>
  );
}
