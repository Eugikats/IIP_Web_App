import { ICON_PATHS } from "@/components/academy/icons.generated";

/** The design's icon set, drawn exactly as the design draws it (stroke icons; play and pause are filled). */
export function Icon({ name, size = 20, className = "" }: { name: string; size?: number; className?: string }) {
  const filled = name === "play" || name === "pause";
  return (
    <svg
      className={`ic ${className}`.trim()}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={filled ? "currentColor" : "none"}
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      dangerouslySetInnerHTML={{ __html: ICON_PATHS[name] ?? "" }}
    />
  );
}
