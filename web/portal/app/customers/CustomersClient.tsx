"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import { ArrowRight, Search, UserRound } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { PageHeader } from "@/components/PageHeader";
import { useRole } from "@/components/RoleContext";
import { SectionCard } from "@/components/SectionCard";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type CaseDto = {
  caseId: string;
  customerName: string;
  customerEmail?: string | null;
  customerPhone?: string | null;
  policyNumber: string;
  productLine: string;
  claimType: string;
  channel: string;
  status: string;
  completenessScore?: number;
  updatedAt?: string;
};

type CustomerDto = {
  customerId: string;
  fullName: string;
  email?: string | null;
  phone?: string | null;
};

type Communication = {
  communicationId: string;
  caseId?: string | null;
  channel: string;
  messageBody: string;
  deliveryStatus?: string;
  createdAt: string;
};

type CustomerView = {
  customerId: string;
  name: string;
  email: string;
  phone: string;
  cases: CaseDto[];
  communications: Communication[];
};

export function CustomersClient() {
  const { role, userId } = useRole();
  const [customersSource, setCustomersSource] = useState<CustomerDto[]>([]);
  const [cases, setCases] = useState<CaseDto[]>([]);
  const [communications, setCommunications] = useState<Communication[]>([]);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const headers = scopedHeaders(role, userId);
    try {
      const [customerResponse, caseResponse, communicationResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/customers`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/communications/history`, { headers }),
      ]);
      if (!customerResponse.ok) throw new Error(`Customers failed with HTTP ${customerResponse.status}.`);
      if (!caseResponse.ok) throw new Error(`Cases failed with HTTP ${caseResponse.status}.`);
      setCustomersSource((await customerResponse.json()) as CustomerDto[]);
      setCases((await caseResponse.json()) as CaseDto[]);
      if (communicationResponse.ok) setCommunications((await communicationResponse.json()) as Communication[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Customers failed to load.");
    } finally {
      setLoading(false);
    }
  }, [role, userId]);

  useEffect(() => {
    load();
  }, [load]);

  const customers = useMemo(() => {
    const byId = new Map<string, CustomerView>();
    for (const customer of customersSource) {
      const matchingCases = cases.filter((claim) => matchesCustomer(claim, customer));
      byId.set(customer.customerId, {
        customerId: customer.customerId,
        name: customer.fullName || "Unknown customer",
        email: customer.email ?? "",
        phone: customer.phone ?? "",
        cases: matchingCases,
        communications: [],
      });
    }

    for (const communication of communications) {
      const claim = cases.find((item) => item.caseId === communication.caseId);
      if (!claim) continue;
      const customer = customersSource.find((item) => matchesCustomer(claim, item));
      if (!customer) continue;
      byId.get(customer.customerId)?.communications.push(communication);
    }
    return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
  }, [cases, communications, customersSource]);

  const filtered = customers.filter((customer) => {
    const needle = query.trim().toLowerCase();
    if (!needle) return true;
    return [customer.name, customer.email, customer.phone, ...customer.cases.map((item) => item.policyNumber)]
      .filter(Boolean)
      .some((value) => value.toLowerCase().includes(needle));
  });

  if (loading) return <LoadingState label="Loading customer 360..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }}>
      <PageHeader
        eyebrow="Customer 360"
        title="Customers"
        description="Real customer read-view assembled from case and communication records already present in the workspace."
      />
      {error ? <div className="mb-4"><ErrorState message={error} /></div> : null}

      <SectionCard>
        <label className="flex items-center gap-3 rounded-xl border border-line bg-shell px-3 py-2">
          <Search className="h-4 w-4 text-muted" />
          <input
            className="w-full bg-transparent text-sm text-ink outline-none placeholder:text-muted"
            placeholder="Search by customer, email, phone, or policy"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
      </SectionCard>

      <div className="mt-5">
        {filtered.length === 0 ? (
          <EmptyState title="No customers found" description="Customers appear here when cases exist in the current tenant workspace." />
        ) : (
          <DataTable label="Customers">
            <thead className="bg-mist text-left text-xs uppercase tracking-wider text-muted">
              <tr>
                <th className="px-4 py-3">Customer</th>
                <th className="px-4 py-3">Cases</th>
                <th className="px-4 py-3">Latest status</th>
                <th className="px-4 py-3">Channels</th>
                <th className="px-4 py-3">Open</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {filtered.map((customer) => {
                const latest = customer.cases[0];
                const channels = Array.from(new Set(customer.cases.map((item) => item.channel).filter(Boolean)));
                return (
                  <tr key={customer.customerId}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="flex h-9 w-9 items-center justify-center rounded-lg border border-ai-border bg-ai-soft text-ai">
                          <UserRound className="h-4 w-4" />
                        </div>
                        <div>
                          <p className="font-semibold text-ink">{customer.name}</p>
                          <p className="font-mono text-xs text-muted">{customer.email || customer.phone || "Contact not configured"}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-muted">{customer.cases.length}</td>
                    <td className="px-4 py-3"><StatusBadge label={latest?.status ?? "Not configured"} /></td>
                    <td className="px-4 py-3 text-sm text-muted">{channels.join(", ") || "Not configured"}</td>
                    <td className="px-4 py-3">
                      <Link className="inline-flex items-center gap-1 text-sm font-semibold text-ai" href={`/customers/${encodeURIComponent(customer.customerId)}`}>View 360 <ArrowRight className="h-3.5 w-3.5" /></Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </DataTable>
        )}
      </div>
    </motion.div>
  );
}

function matchesCustomer(claim: CaseDto, customer: CustomerDto) {
  const emailMatches = customer.email && claim.customerEmail && customer.email.toLowerCase() === claim.customerEmail.toLowerCase();
  if (emailMatches) return true;

  const phoneMatches = customer.phone && claim.customerPhone && normalizePhone(customer.phone) === normalizePhone(claim.customerPhone);
  if (phoneMatches) return true;

  return !customer.email && !customer.phone && customer.fullName.toLowerCase() === (claim.customerName || "").toLowerCase();
}

function normalizePhone(value: string) {
  return value.replace(/\D/g, "");
}
