// The marks of the sign-in providers, as CIP draws them on its sign-in screen, and the integration logos CIP vendors
// under /logos/integrations. Brand marks are drawn in their own colours and never recoloured.

import type { ReactNode } from "react";
import { Mail } from "lucide-react";

export function GoogleIcon({ className }: { className?: string }) {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true" className={className ?? "shrink-0"}>
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.3 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.1 8 3l5.7-5.7C34.2 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.5-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 16 19 12 24 12c3.1 0 5.8 1.1 8 3l5.7-5.7C34.2 6.1 29.3 4 24 4 16.1 4 9.2 8.5 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.3 35.3 26.8 36 24 36c-5.3 0-9.7-3.3-11.3-8l-6.5 5C9.1 39.5 16 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.1-4.1 5.5l.1.1 6.2 5.2C39.2 36.5 44 31 44 24c0-1.3-.1-2.5-.4-3.5z" />
    </svg>
  );
}

export function MicrosoftIcon({ className }: { className?: string }) {
  return (
    <svg width="18" height="18" viewBox="0 0 21 21" aria-hidden="true" className={className ?? "shrink-0"}>
      <rect x="1" y="1" width="9" height="9" fill="#f25022" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
    </svg>
  );
}

export function EmailCodeIcon({ className }: { className?: string }) {
  return <Mail className={className ?? "h-[18px] w-[18px] shrink-0 text-slate-600"} aria-hidden="true" />;
}

const FILE_BY_ID: Record<string, string> = {
  whatsapp: "/logos/integrations/whatsapp.svg",
  sms: "/logos/integrations/sms.svg",
  email: "/logos/integrations/outlook-icon.png",
  mailbox: "/logos/integrations/outlook-icon.png",
  outlook: "/logos/integrations/outlook-icon.png",
  gmail: "/logos/integrations/gmail-icon.png",
  teams: "/logos/integrations/teams.svg",
  gdrive: "/logos/integrations/google-drive-icon.png",
  google_drive: "/logos/integrations/google-drive-icon.png",
  onedrive: "/logos/integrations/onedrive-icon.png",
  sharepoint: "/logos/integrations/sharepoint-icon.png",
  web_chat: "/logos/integrations/web-chat.svg",
  data_hub: "/logos/integrations/data-hub.svg",
  power_bi: "/logos/integrations/power-bi.svg",
  fabric: "/logos/integrations/fabric.svg",
  azure_openai: "/logos/integrations/azure-openai.svg",
};

/** An integration's logo in a small white tile. Returns null for an id with no logo, so the caller keeps its own icon. */
export function IntegrationBrandIcon({ id, fallback, className }: { id: string; fallback?: ReactNode; className?: string }) {
  const src = FILE_BY_ID[id.toLowerCase()];
  if (!src) return <>{fallback ?? null}</>;
  return (
    <span aria-hidden className={`inline-flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white p-1 ring-1 ring-slate-200/80 ${className ?? ""}`}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt="" className="h-full w-full object-contain" />
    </span>
  );
}
