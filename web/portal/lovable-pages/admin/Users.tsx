"use client";

import { useState } from "react";
import { toast } from "sonner";
import { UserPlus } from "lucide-react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
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
          <button
            type="button"
            onClick={() => setShowCreate((v) => !v)}
            className="inline-flex items-center gap-2 rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2"
          >
            <UserPlus className="h-4 w-4" /> Add user
          </button>
        }
      />
      <div className="mb-4">
        <GuardrailBanner text="Roles determine server-side permission — never client display state. The last active tenant administrator cannot be demoted." />
      </div>

      {showCreate ? (
        <GlassCard className="mb-4">
          <div className="mb-3 text-[14px] font-semibold text-ink">New user</div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="User ID" value={form.userId} onChange={(v) => setForm((f) => ({ ...f, userId: v }))} required />
            <Field label="Name" value={form.name} onChange={(v) => setForm((f) => ({ ...f, name: v }))} required />
            <Field label="Email" value={form.email} onChange={(v) => setForm((f) => ({ ...f, email: v }))} type="email" required />
            <Field label="Phone" value={form.phone} onChange={(v) => setForm((f) => ({ ...f, phone: v }))} />
            <label>
              <span className="text-[11.5px] font-medium text-ink-soft">Role *</span>
              <select
                value={form.role}
                onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))}
                className="mt-1 w-full rounded-2xl border border-line bg-white/80 px-4 py-3 text-[13.5px] text-ink outline-none focus:ring-2 focus:ring-info/40"
              >
                <option value="">Select a role…</option>
                {roles.map((r) => (
                  <option key={r.role} value={r.role}>{r.role}</option>
                ))}
              </select>
            </label>
            <Field label="Department" value={form.department} onChange={(v) => setForm((f) => ({ ...f, department: v }))} />
            <Field label="Branch" value={form.branch} onChange={(v) => setForm((f) => ({ ...f, branch: v }))} />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <button type="button" onClick={() => setShowCreate(false)} className="rounded-2xl border border-line bg-white px-4 py-2.5 text-[13px] font-medium text-ink">Cancel</button>
            <button type="button" onClick={submitCreate} disabled={creating} className="rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2 disabled:opacity-50">
              {creating ? "Creating…" : "Create user"}
            </button>
          </div>
        </GlassCard>
      ) : null}

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading users…</div></GlassCard>
      ) : users.length === 0 ? (
        <GlassCard><div className="p-10 text-center text-muted"><div className="text-ink font-medium">No users found</div><div className="text-[12.5px] mt-1">{error ?? "No users are registered for this workspace."}</div></div></GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-muted">
                <th className="p-3">User</th><th className="p-3">Role</th><th className="p-3">Department / branch</th><th className="p-3">Status</th><th className="p-3 text-right">Change role</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const blocked = blockedReasonFor(u);
                return (
                  <tr key={u.userId} className="border-t border-line/70 hover:bg-white/50">
                    <td className="p-3"><div className="text-ink font-medium">{u.name}</div><div className="text-[10.5px] text-muted">{u.email}</div></td>
                    <td className="p-3"><Badge tone="neutral">{u.role}</Badge></td>
                    <td className="p-3 text-ink-soft">{[u.department, u.branch].filter(Boolean).join(" · ") || "—"}</td>
                    <td className="p-3">{u.status === "Active" ? <Badge tone="success">Active</Badge> : <Badge tone="warning">{u.status}</Badge>}</td>
                    <td className="p-3 text-right">
                      <select
                        aria-label={`Change role for ${u.name}`}
                        disabled={!!blocked}
                        title={blocked ?? undefined}
                        value=""
                        onChange={(e) => {
                          if (!e.target.value) return;
                          setPendingRoleChange({ user: u, nextRole: e.target.value });
                          e.target.value = "";
                        }}
                        className="rounded-lg border border-line bg-white px-2 py-1.5 text-[11.5px] text-ink disabled:opacity-50"
                      >
                        <option value="">Change to…</option>
                        {roles.filter((r) => r.role !== u.role).map((r) => (
                          <option key={r.role} value={r.role}>{r.role}</option>
                        ))}
                      </select>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </GlassCard>
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
  return (
    <label>
      <span className="text-[11.5px] font-medium text-ink-soft">{label}{required ? " *" : ""}</span>
      <input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        type={type}
        className="mt-1 w-full rounded-2xl border border-line bg-white/80 px-4 py-3 text-[13.5px] text-ink outline-none focus:ring-2 focus:ring-info/40"
      />
    </label>
  );
}
