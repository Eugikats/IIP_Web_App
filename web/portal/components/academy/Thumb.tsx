import Image from "next/image";
import { Icon } from "@/components/academy/Icon";
import type { CourseSummary } from "@/lib/academy/types";

const courseImages: Record<string, string> = {
  "ai-claims-fraud": "/academy/course-images/ai-claims-fraud.webp",
  "ai-sales-distribution": "/academy/course-images/ai-sales-distribution.webp",
  "ai-underwriting-risk": "/academy/course-images/ai-underwriting-risk.webp",
  "claims-communication": "/academy/course-images/claims-communication.webp",
};

/** Course imagery with a gradient icon for courses without a matching image. */
export function Thumb({ course, badge }: { course: Pick<CourseSummary, "id" | "hue" | "icon">; badge?: string | null }) {
  const image = courseImages[course.id];
  return (
    <div className="thumb" style={{ background: `linear-gradient(135deg,${course.hue[0]},${course.hue[1]})` }}>
      {image ? <Image className="tphoto" src={image} alt="" fill sizes="(max-width: 768px) 100vw, 360px" unoptimized /> : (
        <>
          <svg className="tdeco" viewBox="0 0 300 150" preserveAspectRatio="none" aria-hidden="true">
            <circle cx="255" cy="10" r="80" fill="rgba(255,255,255,.12)" />
            <circle cx="30" cy="160" r="70" fill="rgba(255,255,255,.10)" />
            <path d="M0 120 Q150 70 300 120" stroke="rgba(255,255,255,.18)" strokeWidth="2" fill="none" />
          </svg>
          <div className="ticon"><Icon name={course.icon} size={34} /></div>
        </>
      )}
      {badge ? <span className={`badge${badge === "Completed" ? " green" : ""}`}>{badge}</span> : null}
    </div>
  );
}

/** The percentage ring on the dashboard. */
export function Ring({ percent, size = 40 }: { percent: number; size?: number }) {
  const r = 15;
  const c = 2 * Math.PI * r;
  return (
    <svg width={size} height={size} viewBox="0 0 40 40" aria-hidden="true">
      <circle cx="20" cy="20" r={r} fill="none" stroke="#FCE3B4" strokeWidth="5" />
      <circle cx="20" cy="20" r={r} fill="none" stroke="#F5A524" strokeWidth="5" strokeLinecap="round" strokeDasharray={`${(c * percent) / 100} ${c}`} transform="rotate(-90 20 20)" />
    </svg>
  );
}
