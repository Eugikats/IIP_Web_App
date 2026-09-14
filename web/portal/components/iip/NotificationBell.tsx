"use client";

import { useEffect, useRef, useState } from "react";
import { Bell, CheckCheck, ClipboardCheck, AlertTriangle, TimerReset, FileWarning, MessageCircleQuestion } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { useNotifications, type NotificationView } from "@/lib/lovable-parity/data/notifications";

const TYPE_META: Record<string, { label: string; icon: typeof Bell }> = {
  ApprovalAssigned: { label: "Approval assigned", icon: ClipboardCheck },
  ClaimEscalated: { label: "Claim escalated", icon: AlertTriangle },
  SlaBreach: { label: "SLA breached", icon: TimerReset },
  DocumentReviewNeeded: { label: "Document needs review", icon: FileWarning },
  ReviewRequested: { label: "Review requested", icon: MessageCircleQuestion },
};

function timeAgo(iso: string): string {
  const ms = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(ms / 60000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

// WO-14: the bell previously had no unread indicator at all (removed in WO-02 for being
// unconditional/fake), this reinstates it bound to a real feed.
export default function NotificationBell() {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const { unreadCount, notifications, loading, error, markRead, markAllRead } = useNotifications();

  useEffect(() => {
    if (!open) return;
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false);
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : "Notifications"}
        className="relative hidden h-11 w-11 place-items-center rounded-xl hover:bg-white/70 text-muted hover:text-ink transition-colors focus-ring lg:grid"
      >
        <Bell className="h-4 w-4" />
        {unreadCount > 0 && (
          <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-danger" />
        )}
      </button>

      {open && (
        <div
          role="menu"
          aria-label="Notifications"
          className="absolute right-0 top-[calc(100%+8px)] z-50 w-80 rounded-2xl border border-line bg-white shadow-l3 p-2"
        >
          <div className="flex items-center justify-between px-2 py-1.5">
            <span className="text-[12.5px] font-semibold text-ink">Notifications</span>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => void markAllRead()}
                className="inline-flex items-center gap-1 text-[11px] text-info hover:underline"
              >
                <CheckCheck className="h-3 w-3" /> Mark all read
              </button>
            )}
          </div>
          <div className="max-h-96 overflow-y-auto">
            {loading ? (
              <div className="p-4 text-center text-[12.5px] text-muted">Loading…</div>
            ) : error ? (
              <div className="p-4 text-center text-[12.5px] text-muted">{error}</div>
            ) : notifications.length === 0 ? (
              <div className="p-4 text-center text-[12.5px] text-muted">No notifications.</div>
            ) : (
              notifications.map((notification) => (
                <NotificationRow key={notification.notificationId} notification={notification} onRead={markRead} onNavigate={() => setOpen(false)} />
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function NotificationRow({
  notification, onRead, onNavigate,
}: { notification: NotificationView; onRead: (id: string) => void; onNavigate: () => void }) {
  const meta = TYPE_META[notification.type] ?? { label: notification.type, icon: Bell };
  const Icon = meta.icon;
  return (
    <Link
      to={`/app/claims/${notification.caseId}`}
      onClick={() => {
        onNavigate();
        if (!notification.isRead) onRead(notification.notificationId);
      }}
      className={
        "flex items-start gap-2.5 rounded-xl px-2 py-2.5 hover:bg-line/60 " +
        (notification.isRead ? "" : "bg-info/5")
      }
      role="menuitem"
    >
      <div className={"mt-0.5 h-7 w-7 shrink-0 rounded-lg grid place-items-center " + (notification.isRead ? "bg-line/60 text-muted" : "bg-info/12 text-info")}>
        <Icon className="h-3.5 w-3.5" />
      </div>
      <div className="min-w-0">
        <div className="text-[11px] font-semibold text-ink">{meta.label}</div>
        <div className="text-[12px] text-ink-soft leading-snug">{notification.message}</div>
        <div className="text-[10px] text-muted mt-0.5">Claim {notification.caseId.slice(0, 8)} · {timeAgo(notification.createdAt)}</div>
      </div>
      {!notification.isRead && <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-info" />}
    </Link>
  );
}
