"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { ZenithChat } from "@/components/academy/ZenithChat";
import { ZENITH_READY } from "@/lib/academy/flags";

// Zenith's floating button and panel, as designed. It is for signed-in people, it is not drawn on the full Zenith page,
// and it is hidden while a person is being assessed so it cannot be asked for answers during a quiz or the final task.
export default function ZenithWidget({ route }: { route: string }) {
  const { me, zenithOpen, setZenithOpen } = useAcademy();
  const pathname = usePathname() ?? "";
  const assessing = pathname.startsWith("/academy/assessment") || pathname.startsWith("/academy/final");
  if (!ZENITH_READY || !me?.signedIn || route === "assistant" || assessing) return null;

  return (
    <>
      <button type="button" className="fab" onClick={() => setZenithOpen(true)} aria-label="Open Zenith" id="fab" data-testid="zenith-fab"><Icon name="sparkle" size={28} /></button>
      <div className="aiw" id="aiw" role="dialog" aria-label="Zenith" data-testid="zenith-panel">
        <div className="aiwh">
          <div className="aav"><Icon name="sparkle" size={16} /></div>
          <div>Zenith<small><span className="live" />Your AI learning assistant</small></div>
          <Link className="hb" href="/academy/zenith" title="Open full view" onClick={() => setZenithOpen(false)}><Icon name="expand" size={17} /></Link>
          <button type="button" className="hb" onClick={() => setZenithOpen(false)} title="Minimise" aria-label="Minimise Zenith"><Icon name="minus" size={18} /></button>
        </div>
        {zenithOpen ? <ZenithChat autoFocus /> : null}
      </div>
    </>
  );
}
