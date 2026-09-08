// Design language v2 §8.12. Background chosen deterministically by hashing the user's stable id
// — never their name, and never random per render — from the fixed `dls.avatar.*` token set
// (app/globals.css, tailwind.config.ts). No literal hex here.
import { cn } from "@/lib/utils";

const AVATAR_CLASSES = [
  "bg-dls-avatar-1",
  "bg-dls-avatar-2",
  "bg-dls-avatar-3",
  "bg-dls-avatar-4",
  "bg-dls-avatar-5",
  "bg-dls-avatar-6",
  "bg-dls-avatar-7",
  "bg-dls-avatar-8",
];

function hashToIndex(id: string, length: number): number {
  let hash = 0;
  for (let i = 0; i < id.length; i += 1) {
    hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  }
  return hash % length;
}

export interface AvatarProps {
  id: string;
  initials: string;
  size?: 26 | 28 | 34 | 40;
  presence?: boolean;
  className?: string;
}

const sizeClass: Record<number, string> = {
  26: "h-[26px] w-[26px] text-[11px]",
  28: "h-7 w-7 text-[11px]",
  34: "h-[34px] w-[34px] text-xs",
  40: "h-10 w-10 text-xs",
};

export function Avatar({ id, initials, size = 34, presence, className }: AvatarProps) {
  const colorClass = AVATAR_CLASSES[hashToIndex(id, AVATAR_CLASSES.length)];
  return (
    <span
      className={cn(
        "relative inline-flex shrink-0 items-center justify-center rounded-full font-bold text-dls-text-inverse",
        sizeClass[size],
        colorClass,
        className,
      )}
    >
      {initials}
      {presence && (
        <span
          className="absolute bottom-0 right-0 h-[9px] w-[9px] rounded-full bg-dls-success-dot ring-2 ring-dls-surface"
          aria-hidden="true"
        />
      )}
    </span>
  );
}
