"use client";

import { useCallback, useEffect, useId, useState } from "react";
import { toast } from "sonner";
import { Mail } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select, Field as UIField } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { inviteColleague, listInvitations, revokeInvitation, type InvitationView } from "@/lib/platformAuth";

/** Invite a colleague by email. They choose their own password from the emailed link; nothing is shared with them by hand. */
export function InvitationsCard({ roles }: { roles: { role: string }[] }) {
  const { roleDef } = useUI();
  const actor = roleDef.key;
  const emailId = useId();
  const roleId = useId();
  const [items, setItems] = useState<InvitationView[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    const result = await listInvitations(actor);
    if (result.ok) {
      setItems(result.value);
      setError(null);
    } else {
      setForbidden(result.forbidden === true);
      setError(result.error);
    }
  }, [actor]);

  useEffect(() => {
    void load();
  }, [load]);

  async function send() {
    setBusy(true);
    const result = await inviteColleague(actor, email.trim(), role);
    setBusy(false);
    if (!result.ok) return toast.error(result.error);
    toast.success(`Invitation sent to ${result.value.email}.`);
    setEmail("");
    await load();
  }

  async function withdraw(invitation: InvitationView) {
    const result = await revokeInvitation(actor, invitation.invitationId);
    if (!result.ok) return toast.error(result.error);
    toast.success(`Invitation to ${invitation.email} withdrawn.`);
    await load();
  }

  if (forbidden) return <GlassCard className="mb-4"><NoPermissionStateSurface permission="ManageTenantSettings" /></GlassCard>;

  return (
    <GlassCard className="mb-4">
      <div data-testid="invitations-card" className="mb-1 text-[15px] font-bold text-dls-text-primary">Invite a colleague</div>
      <p className="mb-3 text-[12.5px] text-dls-text-secondary">
        We email a link that works once and expires. They choose their own password; you never see it. The role you pick applies when they join.
      </p>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_auto] sm:items-end">
        <UIField label="Email" htmlFor={emailId}>
          <Input id={emailId} type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="off" />
        </UIField>
        <UIField label="Role" htmlFor={roleId}>
          <Select id={roleId} value={role} onChange={(event) => setRole(event.target.value)} className="w-full">
            <option value="">Select a role...</option>
            {roles.map((r) => (
              <option key={r.role} value={r.role}>{r.role}</option>
            ))}
          </Select>
        </UIField>
        <Button variant="primary" onClick={send} disabled={busy || !email.trim() || !role} icon={<Mail aria-hidden="true" />}>
          {busy ? "Sending..." : "Send invitation"}
        </Button>
      </div>

      <div className="mt-4">
        {error ? (
          <ErrorStateSurface title="Invitations could not be loaded" message={error} />
        ) : items === null ? (
          <LoadingSkeleton rows={2} />
        ) : items.length === 0 ? (
          <EmptyStateSurface title="No invitations yet" message="Invitations you send appear here until they are used or expire." />
        ) : (
          <Table label="Invitations">
            <TableHead>
              <TableRow>
                <TableHeaderCell>Email</TableHeaderCell>
                <TableHeaderCell>Role</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell>Expires</TableHeaderCell>
                <TableHeaderCell numeric>Action</TableHeaderCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.invitationId}>
                  <TableCell>{item.email}</TableCell>
                  <TableCell><Badge tone="neutral">{item.role}</Badge></TableCell>
                  <TableCell><Badge tone={item.status === "Pending" ? "warning" : item.status === "Accepted" ? "success" : "neutral"}>{item.status}</Badge></TableCell>
                  <TableCell className="text-dls-text-secondary">{new Date(item.expiresAt).toLocaleDateString()}</TableCell>
                  <TableCell numeric>
                    {item.status === "Pending" ? <Button variant="ghost" onClick={() => void withdraw(item)}>Withdraw</Button> : null}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </GlassCard>
  );
}
