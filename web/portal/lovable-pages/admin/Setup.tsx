"use client";

import { useCallback, useEffect, useState } from "react";
import { Check, Circle } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Input, Select, Textarea } from "@/components/ui/Input";
import { ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { getFileConnectionStatus, type FileConnectionStatus } from "@/lib/files/fileConnections";
import {
  acknowledgeSecurity,
  acceptTerms,
  getChecklist,
  getProfile,
  getSecurity,
  saveProfile,
  type CompanyProfile,
  type ProfileState,
  type SecurityState,
  type SetupChecklist,
} from "@/lib/orgSetup";

type Form = Omit<CompanyProfile, "updatedAt" | "updatedBy">;

const EMPTY: Form = {
  legalName: "",
  tradingName: "",
  licenceKind: "Insurer",
  licenceNumber: "",
  regulator: "Insurance Regulatory Authority of Uganda",
  country: "Uganda",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  website: "",
  about: "",
};

const KIND_LABEL: Record<Form["licenceKind"], string> = {
  Insurer: "Insurer",
  Broker: "Broker",
  Agent: "Insurance agent",
  BancassuranceDistributor: "Bank selling insurance for partner insurers",
};

/** The steps a new organisation takes before the assistant serves its customers, worked out from what exists, with the forms for the first
 *  two: who the organisation is, and a named person's acceptance of the terms and the data processing agreement. */
export default function Setup() {
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [checklist, setChecklist] = useState<SetupChecklist | null>(null);
  const [state, setState] = useState<ProfileState | null>(null);
  const [security, setSecurity] = useState<SecurityState | null>(null);
  const [fileConnections, setFileConnections] = useState<FileConnectionStatus[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [form, setForm] = useState<Form>(EMPTY);
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [acceptor, setAcceptor] = useState({ fullName: "", title: "", authorised: false });
  const [acceptError, setAcceptError] = useState<string | null>(null);
  const [securityName, setSecurityName] = useState("");
  const [securityError, setSecurityError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [steps, profile, securityResult, files] = await Promise.all([
      getChecklist(role), getProfile(role), getSecurity(role), getFileConnectionStatus(role).catch(() => null),
    ]);
    setLoading(false);
    if (!steps.ok || !profile.ok || !securityResult.ok) {
      const failed = !steps.ok ? steps : !profile.ok ? profile : securityResult as { ok: false; error: string; forbidden: boolean };
      setForbidden(failed.forbidden);
      setError(failed.forbidden ? null : failed.error);
      return;
    }
    setChecklist(steps.value);
    setState(profile.value);
    setSecurity(securityResult.value);
    setFileConnections(files);
    if (profile.value.profile) {
      const { updatedAt: _updatedAt, updatedBy: _updatedBy, ...rest } = profile.value.profile;
      setForm({ ...EMPTY, ...rest, tradingName: rest.tradingName ?? "", contactPhone: rest.contactPhone ?? "", website: rest.website ?? "" });
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitProfile(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFormError(null);
    setNotice(null);
    const result = await saveProfile(role, {
      ...form,
      tradingName: form.tradingName?.trim() || null,
      contactPhone: form.contactPhone?.trim() || null,
      website: form.website?.trim() || null,
    });
    setBusy(false);
    if (!result.ok) {
      setFormError(result.error);
      return;
    }
    setNotice("Your organisation's profile was saved.");
    await load();
  }

  async function submitAcceptance(event: React.FormEvent) {
    event.preventDefault();
    if (!state) return;
    setBusy(true);
    setAcceptError(null);
    setNotice(null);
    const result = await acceptTerms(role, { ...acceptor, termsVersion: state.termsVersion, dataProcessingVersion: state.dataProcessingVersion });
    setBusy(false);
    if (!result.ok) {
      setAcceptError(result.error);
      return;
    }
    setNotice(`Accepted by ${result.value.acceptedByName} for your organisation.`);
    await load();
  }

  async function submitSecurityAcknowledgement(event: React.FormEvent) {
    event.preventDefault();
    if (!security) return;
    setBusy(true);
    setSecurityError(null);
    setNotice(null);
    const result = await acknowledgeSecurity(role, { fullName: securityName, version: security.version });
    setBusy(false);
    if (!result.ok) {
      setSecurityError(result.error);
      return;
    }
    setNotice(`Read confirmation recorded for ${result.value?.acknowledgedByName ?? securityName}.`);
    await load();
  }

  const patch = (changes: Partial<Form>) => setForm((current) => ({ ...current, ...changes }));

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Set-up"
        title="Set up your organisation"
        sub="What the assistant needs before it speaks to your customers. Each step is ticked only when the thing it asks for exists."
      />

      {loading ? (
        <GlassCard>
          <LoadingSkeleton rows={5} />
        </GlassCard>
      ) : null}
      {forbidden ? (
        <GlassCard>
          <NoPermissionStateSurface permission="ManageTenantSettings" />
        </GlassCard>
      ) : null}
      {error ? (
        <GlassCard>
          <ErrorStateSurface title="The set-up steps could not be loaded" message={error} onRetry={() => void load()} />
        </GlassCard>
      ) : null}

      {notice ? (
        <div className="mb-3">
          <GlassCard>
            <p className="px-1 text-[13.5px] text-dls-text-secondary" role="status">{notice}</p>
          </GlassCard>
        </div>
      ) : null}

      {checklist && state && security ? (
        <div className="space-y-4">
          <GlassCard>
            <h2 className="m-0 text-[15px] font-bold text-dls-text-primary">How your data is protected</h2>
            <p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">Current statements, version {security.version}. Each line describes what is in place today.</p>
            <ul className="m-0 mt-3 space-y-3 p-0">
              {security.statements.map((item) => {
                const label = item.status === "InPlace" ? "In place" : item.status === "Partly" ? "Partly" : "Not offered";
                return <li key={item.key} className="list-none border-t border-dls-border-subtle pt-3">
                  <div className="flex flex-wrap items-center gap-2"><h3 className="m-0 text-[13.5px] font-semibold text-dls-text-primary">{item.title}</h3><span className="rounded-full border border-dls-border-subtle px-2 py-0.5 text-[11.5px] text-dls-text-secondary">{label}</span></div>
                  <p className="m-0 mt-1 text-[12.5px] text-dls-text-secondary">{item.statement}</p>
                </li>;
              })}
            </ul>
            {security.readCurrent && security.acknowledgement ? (
              <p className="mb-0 mt-4 text-[13px] text-dls-text-primary" role="status">Read by <strong>{security.acknowledgement.acknowledgedByName}</strong> on {new Date(security.acknowledgement.acknowledgedAt).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}.</p>
            ) : (
              <form onSubmit={submitSecurityAcknowledgement} className="mt-4 flex flex-wrap items-end gap-3" aria-label="Confirm reading security statements">
                {security.acknowledgement ? <p className="m-0 w-full text-[12.5px] text-dls-text-secondary">These statements changed since {security.acknowledgement.acknowledgedByName} read them. Please read this version.</p> : null}
                <Field label="Your full name" htmlFor="security-reader"><Input id="security-reader" required minLength={2} maxLength={100} value={securityName} onChange={(event) => setSecurityName(event.target.value)} /></Field>
                <Button type="submit" variant="primary" disabled={busy}>I have read this</Button>
                {securityError ? <p className="m-0 w-full text-[13px] text-dls-danger-text" role="alert">{securityError}</p> : null}
              </form>
            )}
          </GlassCard>

          <GlassCard>
            <h2 className="m-0 text-[15px] font-bold text-dls-text-primary">Where your documents come from</h2>
            <p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">Connect the folders your team already uses, or upload documents in the Data Hub. Synced files still need a person to review and approve them.</p>
            {fileConnections === null ? <p className="mb-0 mt-3 text-[12.5px] text-dls-text-muted">Connection status could not be loaded. You can still upload in the Data Hub.</p> : (
              <ul className="m-0 mt-3 space-y-2 p-0">
                {(["microsoft", "google"] as const).map((provider) => {
                  const item = fileConnections.find((connection) => connection.provider === provider);
                  const name = provider === "microsoft" ? "Microsoft (SharePoint and OneDrive)" : "Google Drive";
                  const label = !item?.configured ? "Not configured on this platform yet" : item.connected ? `Connected${item.accountEmail ? ` as ${item.accountEmail}` : ""}` : "Configured, not connected";
                  return <li key={provider} className="flex flex-wrap items-center justify-between gap-2 border-t border-dls-border-subtle pt-2 text-[13px]"><span className="text-dls-text-primary">{name}</span><span className="text-dls-text-secondary">{label}</span></li>;
                })}
              </ul>
            )}
            <Link to="/app/knowledge" className="mt-4 inline-block text-[13px] font-semibold text-dls-primary underline">Upload documents in the Data Hub</Link>
          </GlassCard>
          <GlassCard>
            <p className="mb-2 text-[13px] text-dls-text-muted">{checklist.completed} of {checklist.total} done</p>
            <ol className="m-0 list-none divide-y divide-dls-border-subtle p-0" aria-label="Set-up steps">
              {checklist.steps.map((step) => (
                <li key={step.key} className="flex items-start gap-3 py-3">
                  {step.done ? <Check className="mt-0.5 h-4 w-4 shrink-0 text-dls-success-text" aria-hidden="true" /> : <Circle className="mt-0.5 h-4 w-4 shrink-0 text-dls-text-faint" aria-hidden="true" />}
                  <div className="min-w-0">
                    <p className="m-0 text-[13.5px] font-semibold text-dls-text-primary">
                      {step.title} <span className="font-normal text-dls-text-muted">{step.done ? "Done" : "Not done yet"}</span>
                    </p>
                    <p className="m-0 mt-0.5 text-[12.5px] text-dls-text-secondary">{step.hint}</p>
                    {!step.done && step.route !== "/app/admin/setup" ? (
                      <Link to={step.route} className="mt-1 inline-block text-[12.5px] font-semibold text-dls-primary underline">Open it</Link>
                    ) : null}
                  </div>
                </li>
              ))}
            </ol>
          </GlassCard>

          <GlassCard>
            <h2 className="m-0 text-[15px] font-bold text-dls-text-primary">About your organisation</h2>
            <form onSubmit={submitProfile} className="mt-3 grid gap-3 sm:grid-cols-2" aria-label="About your organisation">
              <Field label="Registered name" htmlFor="co-legal">
                <Input id="co-legal" required value={form.legalName} onChange={(event) => patch({ legalName: event.target.value })} />
              </Field>
              <Field label="Trading name" hint="If customers know you by another name." htmlFor="co-trading">
                <Input id="co-trading" value={form.tradingName ?? ""} onChange={(event) => patch({ tradingName: event.target.value })} />
              </Field>
              <Field label="What kind of licence" htmlFor="co-kind">
                <Select id="co-kind" value={form.licenceKind} onChange={(event) => patch({ licenceKind: event.target.value as Form["licenceKind"] })}>
                  {(Object.keys(KIND_LABEL) as Form["licenceKind"][]).map((kind) => <option key={kind} value={kind}>{KIND_LABEL[kind]}</option>)}
                </Select>
              </Field>
              <Field label="Licence or registration number" hint="As it appears on the certificate." htmlFor="co-licence">
                <Input id="co-licence" required value={form.licenceNumber} onChange={(event) => patch({ licenceNumber: event.target.value })} />
              </Field>
              <Field label="Regulator" htmlFor="co-regulator">
                <Input id="co-regulator" value={form.regulator} onChange={(event) => patch({ regulator: event.target.value })} />
              </Field>
              <Field label="Country" htmlFor="co-country">
                <Input id="co-country" value={form.country} onChange={(event) => patch({ country: event.target.value })} />
              </Field>
              <Field label="Contact person" htmlFor="co-contact">
                <Input id="co-contact" required value={form.contactName} onChange={(event) => patch({ contactName: event.target.value })} />
              </Field>
              <Field label="Contact email" htmlFor="co-email">
                <Input id="co-email" type="email" required value={form.contactEmail} onChange={(event) => patch({ contactEmail: event.target.value })} />
              </Field>
              <Field label="Contact phone" htmlFor="co-phone">
                <Input id="co-phone" inputMode="tel" value={form.contactPhone ?? ""} onChange={(event) => patch({ contactPhone: event.target.value })} />
              </Field>
              <Field label="Website" htmlFor="co-web">
                <Input id="co-web" inputMode="url" placeholder="https://" value={form.website ?? ""} onChange={(event) => patch({ website: event.target.value })} />
              </Field>
              <div className="sm:col-span-2">
                <Field label="About you" hint="Who you serve and what you sell, in 20 to 600 characters. Kato uses it when customers ask who you are." htmlFor="co-about">
                  <Textarea id="co-about" rows={4} required value={form.about} onChange={(event) => patch({ about: event.target.value })} />
                </Field>
              </div>
              {formError ? <p className="m-0 text-[13px] text-dls-danger-text sm:col-span-2" role="alert">{formError}</p> : null}
              <div className="sm:col-span-2">
                <Button type="submit" variant="primary" disabled={busy}>Save the profile</Button>
              </div>
            </form>
          </GlassCard>

          <GlassCard>
            <h2 className="m-0 text-[15px] font-bold text-dls-text-primary">Terms and data processing</h2>
            <p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">
              Read the{" "}
              <a href="/terms" target="_blank" rel="noreferrer" className="font-semibold text-dls-primary underline">Terms and Conditions</a>,{" "}
              the <a href="/privacy" target="_blank" rel="noreferrer" className="font-semibold text-dls-primary underline">Data Privacy &amp; Protection Policy</a> and the{" "}
              <a href="/data-processing" target="_blank" rel="noreferrer" className="font-semibold text-dls-primary underline">Data Processing Agreement</a>. Versions in force: terms {state.termsVersion}, agreement {state.dataProcessingVersion}.
            </p>
            {state.acceptedCurrent && state.acceptance ? (
              <p className="mt-3 text-[13.5px] text-dls-text-primary" role="status">
                Accepted by <strong>{state.acceptance.acceptedByName}</strong>, {state.acceptance.acceptedByTitle}, on {new Date(state.acceptance.acceptedAt).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}.
              </p>
            ) : (
              <form onSubmit={submitAcceptance} className="mt-3 grid gap-3 sm:grid-cols-2" aria-label="Accept the terms">
                {state.acceptance ? (
                  <p className="m-0 text-[13px] text-dls-text-secondary sm:col-span-2">
                    The documents changed since {state.acceptance.acceptedByName} accepted them, so they need to be accepted again.
                  </p>
                ) : null}
                <Field label="Your full name" htmlFor="tc-name">
                  <Input id="tc-name" required value={acceptor.fullName} onChange={(event) => setAcceptor({ ...acceptor, fullName: event.target.value })} />
                </Field>
                <Field label="Your position" htmlFor="tc-title">
                  <Input id="tc-title" required placeholder="Managing Director" value={acceptor.title} onChange={(event) => setAcceptor({ ...acceptor, title: event.target.value })} />
                </Field>
                <label className="flex items-start gap-2 text-[13px] text-dls-text-secondary sm:col-span-2">
                  <input type="checkbox" className="mt-0.5" checked={acceptor.authorised} onChange={(event) => setAcceptor({ ...acceptor, authorised: event.target.checked })} />
                  I am authorised to accept these documents for this organisation.
                </label>
                {acceptError ? <p className="m-0 text-[13px] text-dls-danger-text sm:col-span-2" role="alert">{acceptError}</p> : null}
                <div className="sm:col-span-2">
                  <Button type="submit" variant="primary" disabled={busy || !acceptor.authorised}>Accept for the organisation</Button>
                </div>
              </form>
            )}
          </GlassCard>
        </div>
      ) : null}
    </>
  );
}
