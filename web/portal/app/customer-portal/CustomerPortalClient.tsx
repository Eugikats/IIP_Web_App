"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { EmptyState } from "@/components/States";
import { StatusChip } from "@/components/GovernedUi";
import { FileText, MessageSquare, ShieldCheck, UploadCloud, UserRound } from "lucide-react";

type CustomerPortalView = "home" | "submit" | "claims" | "upload" | "policy" | "messages" | "profile";

const navItems: Array<{ view: CustomerPortalView; label: string; href: string }> = [
  { view: "home", label: "Home", href: "/customer-portal" },
  { view: "submit", label: "Submit Claim", href: "/customer-portal/submit-claim" },
  { view: "claims", label: "My Claims", href: "/customer-portal/my-claims" },
  { view: "upload", label: "Upload Documents", href: "/customer-portal/upload-documents" },
  { view: "policy", label: "Policy Support", href: "/customer-portal/policy-support" },
  { view: "messages", label: "Messages", href: "/customer-portal/messages" },
  { view: "profile", label: "Profile", href: "/customer-portal/profile" }
];

export function CustomerPortalClient({ view }: { view: CustomerPortalView }) {
  const pathname = usePathname();

  return (
    <div className="space-y-6">
      <section className="rounded-3xl border border-line bg-surface p-6 shadow-panel">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Customer Portal</p>
            <h1 className="mt-2 text-3xl font-semibold text-ink">Your claim support workspace</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
              Customer-facing claim status, document upload, policy support and messages will appear only through customer-safe contracts.
            </p>
          </div>
          <StatusChip label="Customer-safe DTOs only" tone="success" />
        </div>
      </section>

      <nav className="flex gap-2 overflow-x-auto rounded-2xl border border-line bg-surface p-2" aria-label="Customer portal">
        {navItems.map((item) => {
          const active = item.view === view || pathname === item.href;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={`whitespace-nowrap rounded-xl px-3 py-2 text-sm font-semibold transition-colors ${active ? "bg-violet/10 text-violet" : "text-muted hover:bg-mist hover:text-ink"}`}
            >
              {item.label}
            </Link>
          );
        })}
      </nav>

      {view === "home" ? <HomeView /> : null}
      {view === "submit" ? <UnavailableWorkflow icon={<FileText className="h-5 w-5" />} title="Submit Claim" description="Customer claim submission is unavailable until a customer-safe intake endpoint is implemented. Staff intake remains separate." /> : null}
      {view === "claims" ? <UnavailableWorkflow icon={<ShieldCheck className="h-5 w-5" />} title="My Claims" description="No customer-safe claim list endpoint is available yet. Staff claim DTOs are not reused or hidden in this customer view." /> : null}
      {view === "upload" ? <UnavailableWorkflow icon={<UploadCloud className="h-5 w-5" />} title="Upload Documents" description="Customer document upload requires a customer-safe upload endpoint and storage policy before it can accept files." /> : null}
      {view === "policy" ? <UnavailableWorkflow icon={<ShieldCheck className="h-5 w-5" />} title="Policy Support" description="Policy questions for customers will be enabled only through approved customer-safe source grounding." /> : null}
      {view === "messages" ? <UnavailableWorkflow icon={<MessageSquare className="h-5 w-5" />} title="Messages" description="Customer messages will appear after a customer-safe message endpoint is implemented." /> : null}
      {view === "profile" ? <UnavailableWorkflow icon={<UserRound className="h-5 w-5" />} title="Profile" description="Profile details require authenticated customer identity and customer-safe account fields." /> : null}
    </div>
  );
}

function HomeView() {
  return (
    <div className="grid gap-5 lg:grid-cols-3">
      <section className="lg:col-span-2 rounded-3xl border border-line bg-surface p-6 shadow-panel">
        <h2 className="text-lg font-semibold text-ink">Claim tracker</h2>
        <div className="mt-4">
          <EmptyState
            title="No customer-safe claim data"
            description="Customer claim status will appear here only after a customer-safe claim endpoint is implemented."
          />
        </div>
      </section>
      <section className="rounded-3xl border border-line bg-surface p-6 shadow-panel">
        <h2 className="text-lg font-semibold text-ink">Next step</h2>
        <p className="mt-3 text-sm leading-6 text-muted">
          A member of the claims team will contact you regarding next steps when customer portal access is configured.
        </p>
        <div className="mt-4">
          <StatusChip label="Not configured" tone="neutral" />
        </div>
      </section>
    </div>
  );
}

function UnavailableWorkflow({ icon, title, description }: { icon: ReactNode; title: string; description: string }) {
  return (
    <section className="rounded-3xl border border-line bg-surface p-6 shadow-panel">
      <div className="flex items-start gap-4">
        <div className="flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-2xl border border-violet/20 bg-violet/10 text-violet">
          {icon}
        </div>
        <div className="min-w-0">
          <h2 className="text-xl font-semibold text-ink">{title}</h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">{description}</p>
          <div className="mt-4">
            <StatusChip label="Not configured" tone="neutral" />
          </div>
        </div>
      </div>
    </section>
  );
}
