"use client";

import { useCallback, useEffect, useState } from "react";
import { nativeDevice, onNativeSettings } from "@/lib/nativeDevice";

/**
 * Two choices that only exist in the Android app: listening for "Hey Kato" with the screen off, and a short check for
 * things that need the person while the app is closed. Neither is offered in a browser, where the card is not drawn.
 * "Hey Kato" is off until switched on here, because it keeps the microphone open; nudges are on, because they only
 * show a notification, and can be switched off here.
 */

type Row = { id: string; title: string; detail: string; on: boolean; change: (wanted: boolean) => void };

function Switch({ on, label, onChange }: { on: boolean; label: string; onChange: (wanted: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      onClick={() => onChange(!on)}
      className="relative flex h-11 w-14 shrink-0 items-center justify-center dls-focus-outline"
    >
      <span className={`flex h-6 w-11 items-center rounded-full border px-0.5 ${on ? "justify-end border-[#1769FF] bg-[#1769FF]" : "justify-start border-dls-border-input bg-dls-surface"}`}>
        <span className={`h-[18px] w-[18px] rounded-full ${on ? "bg-white" : "bg-dls-text-secondary"}`} />
      </span>
    </button>
  );
}

export default function PhoneVoiceSettings() {
  // Whether this is the Android app is only known in the browser, so the first paint draws nothing.
  const [present, setPresent] = useState(false);
  const [wake, setWake] = useState(false);
  const [nudges, setNudges] = useState(true);

  const read = useCallback(() => {
    setWake(nativeDevice.wakeOn());
    setNudges(nativeDevice.nudgesOn());
  }, []);

  useEffect(() => {
    setPresent(nativeDevice.available());
    read();
    return onNativeSettings(read);
  }, [read]);

  if (!present) return null;

  const rows: Row[] = [
    {
      id: "wake",
      title: "Say \"Hey Kato\" to talk",
      detail: "Listens on this phone while the screen is off and opens a conversation when it hears the phrase. A short clip around the phrase is checked by IIP and is not kept. A notification shows while it is on. It asks to use the microphone.",
      on: wake,
      change: (wanted) => { setWake(wanted); nativeDevice.setWake(wanted); },
    },
    {
      id: "nudges",
      title: "Tell me when something needs me",
      detail: "Checks about every 40 minutes on weekdays, 08:00 to 18:00 Kampala time, even when IIP is closed, and shows a notification. On a locked phone it leaves out names and figures.",
      on: nudges,
      change: (wanted) => { setNudges(wanted); nativeDevice.setNudges(wanted); },
    },
  ];

  return (
    <section aria-labelledby="phone-voice-title" data-testid="phone-voice" className="mt-6 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 sm:p-5">
      <h2 id="phone-voice-title" className="m-0 text-[15px] font-semibold text-dls-text-primary">This phone</h2>
      <p className="m-0 mt-1 text-[13px] leading-relaxed text-dls-text-secondary">Choices for the IIP app on this phone only.</p>
      <ul className="m-0 mt-3 list-none divide-y divide-dls-border-subtle p-0">
        {rows.map((row) => (
          <li key={row.id} className="flex items-start gap-3 py-3 first:pt-0 last:pb-0" data-testid={`phone-voice-${row.id}`}>
            <div className="min-w-0 flex-1">
              <p className="m-0 text-[13px] font-semibold text-dls-text-primary">{row.title}</p>
              <p className="m-0 mt-1 text-[13px] leading-relaxed text-dls-text-secondary">{row.detail}</p>
            </div>
            <Switch on={row.on} label={row.title} onChange={row.change} />
          </li>
        ))}
      </ul>
    </section>
  );
}
