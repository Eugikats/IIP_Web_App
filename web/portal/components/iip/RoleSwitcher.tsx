import { useUI } from "@/context/UIStateContext";
import { ROLES, type Role } from "@/lib/roles";
import { UserCog } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";

export default function RoleSwitcher() {
  const { role, setRole, roleDef } = useUI();
  const nav = useNavigate();
  return (
    <label className="hidden md:flex items-center gap-2 text-[12px] text-slate2 rounded-xl border border-cloud bg-white/70 px-2.5 py-1.5 hover-lift cursor-pointer">
      <UserCog className="h-3.5 w-3.5 text-violet2" />
      <span className="text-stone">Preview role:</span>
      <select
        value={role}
        onChange={(e) => {
          const next = e.target.value as Role;
          setRole(next);
          nav(ROLES[next].landing);
        }}
        className="bg-transparent outline-none text-navy font-medium pr-1"
      >
        {Object.values(ROLES).map((r) => (
          <option key={r.key} value={r.key}>{r.label}</option>
        ))}
      </select>
      <span className="text-[10.5px] text-stone">· {roleDef.title}</span>
    </label>
  );
}
