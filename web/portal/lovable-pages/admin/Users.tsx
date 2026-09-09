"use client";

import { useId, useState } from "react";
import { toast } from "sonner";
import { UserPlus } from "lucide-react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select, Field as UIField } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { useAdminUsers, type AdminUserView } from "@/lib/lovable-parity/data/adminUsers";

// WO-12: real UI for the previously-unreachable admin/users endpoints. Roles come from
// GET /api/v1/admin/roles (server-derived), never a hardcoded client list — see useAdminUsers.
export default function Users() {
  const { users, roles, loading, error, createUser, updateUserRole } = useAdminUsers();
  const [showCreate, setShowCreate] = useState(false);
  const [pendingRoleChange, setPendingRoleChange] = useState<{ user: AdminUserView; nextRole: string } | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ userId: "", name: "", email: "", phone: "", role: "", department: "", branch: "" });
  const roleFieldId = useId();

  async function submitCreate() {
    if (!form.userId.trim() || !form.name.trim() || !form.email.trim() || !form.role) {
      toast.error("User ID, name, email, and role are required.");
      return;
    }
    setCreating(true);
    const result = await createUser({
      userId: form.userId.trim(),
      name: form.name.trim(),
      email: form.email.trim(),
      phone: form.phone.trim() || undefined,
      role: form.role,
      department: form.department.trim() || undefined,
      branch: form.branch.trim() || undefined,
    });
    setCreating(false);
    if (result.ok) {
      toast.success("User created.");
      setShowCreate(false);
      setForm({ userId: "", name: "", email: "", phone: "", role: "", department: "", branch: "" });
    } else {
      toast.error(result.message ?? "Create user failed.");
    }
  }

  function blockedReasonFor(user: AdminUserView): string | null {
    return user.blockedActions.find((a) => a.action === "updateUserRole")?.reason ?? null;
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Tenant"
        title="Users & roles"
        sub="Resolved from server-side membership and permission data. Role changes are audited."
        action={
          <Button variant="primary" onClick={() => setShowCreate((v) => !v)} icon={<UserPlus aria-hidden="true" />}>
            Add user
          </Button>
        }
      />
      <div className="mb-4">
        <GuardrailBanner text="Roles determine server-side permission — never client display state. The last active tenant administrator cannot be demoted." />
      </div>

      {showCreate ? (
        <GlassCard className="mb-4">
          <div className="mb-3 text-[15px] font-bold text-dls-text-primary">New user</div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="User ID" value={form.userId} onChange={(v) => setForm((f) => ({ ...f, userId: v }))} required />
            <Field label="Name" value={form.name} onChange={(v) => setForm((f) => ({ ...f, name: v }))} required />
            <Field label="Email" value={form.email} onChange={(v) => setForm((f) => ({ ...f, email: v }))} type="email" required />
            <Field label="Phone" value={form.phone} onChange={(v) => setForm((f) => ({ ...f, phone: v }))} />
            <UIField label="Role *" htmlFor={roleFieldId}>
              <Select id={roleFieldId} value={form.role} onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))} className="w-full">
                <option value="">Select a role…</option>
                {roles.map((r) => (
                  <option key={r.role} value={r.role}>{r.role}</option>
                ))}
              </Select>
            </UIField>
            <Field label="Department" value={form.department} onChange={(v) => setForm((f) => ({ ...f, department: v }))} />
            <Field label="Branch" value={form.branch} onChange={(v) => setForm((f) => ({ ...f, branch: v }))} />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setShowCreate(false)}>Cancel</Button>
            <Button variant="primary" onClick={submitCreate} disabled={creating}>
              {creating ? "Creating…" : "Create user"}
            </Button>
          </div>
        </GlassCard>
      ) : null}

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Users could not be loaded" message={error} /></GlassCard>
      ) : users.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No users found" message="No users are registered for this workspace." /></GlassCard>
      ) : (
        <Table label="Users and roles">
          <TableHead>
            <TableRow>
              <TableHeaderCell>User</TableHeaderCell>
              <TableHeaderCell>Role</TableHeaderCell>
              <TableHeaderCell>Department / branch</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell numeric>Change role</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => {
              const blocked = blockedReasonFor(u);
              return (
                <TableRow key={u.userId}>
                  <TableCell>
                    <div className="font-semibold text-dls-text-primary">{u.name}</div>
                    <div className="text-[11px] text-dls-text-muted">{u.email}</div>
                  </TableCell>
                  <TableCell><Badge tone="neutral">{u.role}</Badge></TableCell>
                  <TableCell className="text-dls-text-secondary">{[u.department, u.branch].filter(Boolean).join(" · ") || "—"}</TableCell>
                  <TableCell>{u.status === "Active" ? <Badge tone="success">Active</Badge> : <Badge tone="warning">{u.status}</Badge>}</TableCell>
                  <TableCell numeric>
                    <Select
                      aria-label={`Change role for ${u.name}`}
                      disabled={!!blocked}
                      title={blocked ?? undefined}
                      value=""
                      onChange={(e) => {
                        if (!e.target.value) return;
                        setPendingRoleChange({ user: u, nextRole: e.target.value });
                        e.target.value = "";
                      }}
                    >
                      <option value="">Change to…</option>
                      {roles.filter((r) => r.role !== u.role).map((r) => (
                        <option key={r.role} value={r.role}>{r.role}</option>
                      ))}
                    </Select>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}

      <GovernedActionDialog
        open={!!pendingRoleChange}
        title="Change user role"
        target={pendingRoleChange ? `${pendingRoleChange.user.name} (${pendingRoleChange.user.email})` : ""}
        impact={pendingRoleChange ? `Role changes from ${pendingRoleChange.user.role} to ${pendingRoleChange.nextRole}. This changes the user's real server-side permissions immediately.` : ""}
        auditNotice="This role change is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel="Confirm role change"
        tone="primary"
        onClose={() => setPendingRoleChange(null)}
        onConfirm={async (reason) => {
          if (!pendingRoleChange) return;
          const result = await updateUserRole(pendingRoleChange.user.userId, pendingRoleChange.nextRole, reason);
          if (result.ok) {
            toast.success(`Role updated for ${pendingRoleChange.user.name}.`);
          } else {
            toast.error(result.message ?? "Role update failed.");
          }
          setPendingRoleChange(null);
        }}
      />
    </>
  );
}

function Field({
  label, value, onChange, type = "text", required = false,
}: { label: string; value: string; onChange: (v: string) => void; type?: string; required?: boolean }) {
  const id = useId();
  return (
    <UIField label={required ? `${label} *` : label} htmlFor={id}>
      <Input id={id} value={value} onChange={(e) => onChange(e.target.value)} type={type} />
    </UIField>
  );
}
