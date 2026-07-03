import {
  BarChart3,
  BookOpen,
  Bot,
  ClipboardCheck,
  FilePlus2,
  FileSearch,
  Headset,
  LifeBuoy,
  LockKeyhole,
  LucideIcon,
  MessageSquare,
  Network,
  ShieldCheck,
  Target,
  Users,
  Workflow
} from "lucide-react";
import type { NavIconKey } from "@/lib/roleUiConfig";

export const navigationIcons: Record<NavIconKey, LucideIcon> = {
  messageSquare: MessageSquare,
  users: Users,
  target: Target,
  clipboardCheck: ClipboardCheck,
  filePlus: FilePlus2,
  fileSearch: FileSearch,
  bot: Bot,
  headset: Headset,
  workflow: Workflow,
  bookOpen: BookOpen,
  network: Network,
  barChart: BarChart3,
  shield: ShieldCheck,
  lock: LockKeyhole,
  lifeBuoy: LifeBuoy
};
