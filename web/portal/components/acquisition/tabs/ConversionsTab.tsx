"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { getCampaigns, getConversions, type ConversionReport, type RuleResults } from "@/lib/rules";
import { KpiRow, ReportState, TableCard, shortWhen, th } from "./ReportParts";

/** What the offers led to, by insurer, for the bank's own people. Conversions only: the bank applies its own commission rates, IIP
 *  records none, and this view is never shown to a customer. */
export default function ConversionsTab() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [report, setReport] = useState<ConversionReport | null>(null);
  const [campaigns, setCampaigns] = useState<RuleResults[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [result, results] = await Promise.all([getConversions(role), getCampaigns(role)]);
    if (results.ok) setCampaigns(results.value);
    setLoading(false);
    if (!result.ok) {
      setForbidden(result.forbidden);
      setError(result.forbidden ? null : result.error);
      return;
    }
    setReport(result.value);
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const total = report?.total;

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Offers sent", value: String(total?.offers ?? 0) },
          { label: "People who answered", value: String(total?.replied ?? 0) },
          { label: "Asked for details", value: String(total?.askedForDetails ?? 0) },
          { label: "Went on to a cover", value: String(total?.convertedAfterOffer ?? 0), helper: "Applied, or hold a policy that began after the offer" },
        ]}
      />

      <TableCard title="By insurer" subtitle={report?.note ?? "Conversions only. IIP does not record commission rates."}>
        <ReportState
          loading={loading}
          error={error}
          forbidden={forbidden}
          onRetry={() => void load()}
          permission="ManageSalesLeads"
          empty={report && report.insurers.length === 0 ? { title: "Nothing to count yet", message: "When a rule sends an offer, it appears here with how people answered and who went on to take cover." } : null}
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[36rem] text-left text-[13px]">
              <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wide text-slate-500">
                <tr>
                  <th className={th}>Insurer</th>
                  <th className={th}>Offers sent</th>
                  <th className={th}>Answered</th>
                  <th className={th}>Asked for details</th>
                  <th className={th}>Went on to a cover</th>
                </tr>
              </thead>
              <tbody>
                {(report?.insurers ?? []).map((row) => (
                  <tr key={row.insurer} className="border-b border-slate-50 last:border-0">
                    <td className="px-3 py-3 font-medium text-[#0B1640]">{row.insurer}</td>
                    <td className="px-3 py-3 text-slate-600">{row.offers}</td>
                    <td className="px-3 py-3 text-slate-600">{row.replied}</td>
                    <td className="px-3 py-3 text-slate-600">{row.askedForDetails}</td>
                    <td className="px-3 py-3 text-slate-600">{row.convertedAfterOffer}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportState>
      </TableCard>

      <TableCard
        title="Results by rule"
        subtitle="From first look to cover. Delivered and read are WhatsApp's own reports for real sends; a message shown as sent in a demonstration is never counted as delivered."
      >
        {campaigns.length === 0 ? (
          <p className="m-0 px-5 py-8 text-center text-[13px] text-slate-500">No rule has looked at anyone yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[52rem] text-left text-[13px]" aria-label="Results by rule">
              <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wide text-slate-500">
                <tr>
                  <th className={th}>Rule</th>
                  <th className={th}>Looked at</th>
                  <th className={th}>Sent</th>
                  <th className={th}>Shown as sent</th>
                  <th className={th}>Delivered</th>
                  <th className={th}>Read</th>
                  <th className={th}>Answered</th>
                  <th className={th}>Went on to a cover</th>
                  <th className={th}>Not sent, and why</th>
                </tr>
              </thead>
              <tbody>
                {campaigns.map((row) => (
                  <tr key={row.ruleId} className="border-b border-slate-50 align-top last:border-0">
                    <td className="px-3 py-3 font-medium text-[#0B1640]">{row.ruleName}</td>
                    <td className="px-3 py-3 text-slate-600">{row.considered}</td>
                    <td className="px-3 py-3 text-slate-600">{row.sent}</td>
                    <td className="px-3 py-3 text-slate-600">{row.shownAsSent}</td>
                    <td className="px-3 py-3 text-slate-600">{row.delivered}</td>
                    <td className="px-3 py-3 text-slate-600">{row.read}</td>
                    <td className="px-3 py-3 text-slate-600">{row.answered}</td>
                    <td className="px-3 py-3 text-slate-600">{row.convertedAfterOffer}</td>
                    <td className="px-3 py-3 text-slate-600">
                      {row.notSent + row.couldNotSend + row.waiting === 0 ? "None" : (
                        <>
                          {row.whyNotSent.map((item) => <span key={item.reason} className="block">{item.people} × {item.reason}</span>)}
                          {row.couldNotSend > 0 ? <span className="block">{row.couldNotSend} could not be sent</span> : null}
                          {row.waiting > 0 ? <span className="block">{row.waiting} waiting for their time</span> : null}
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </TableCard>

      <TableCard title="Who went on to a cover" subtitle={`Applications on record in this workspace: ${report?.applications ?? 0}.`}>
        {report && report.converted.length === 0 ? (
          <p className="m-0 px-5 py-8 text-center text-[13px] text-slate-500">No one offered a cover has taken one up yet.</p>
        ) : (
          <ul className="m-0 list-none divide-y divide-slate-100 p-0" aria-label="People who went on to a cover">
            {(report?.converted ?? []).map((person) => (
              <li key={`${person.phone}-${person.insurer}`} className="px-4 py-3 text-[13px] text-slate-600 sm:px-5">
                <span className="font-semibold text-[#0B1640]">{person.customerName ?? "Unknown"}</span> · {person.cover} ({person.insurer}) · {person.how}. Offered {shortWhen(person.offeredAt)}.
              </li>
            ))}
          </ul>
        )}
      </TableCard>
    </div>
  );
}
