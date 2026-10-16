"use client";

import { useEffect, useState } from "react";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { Icon } from "@/components/academy/Icon";
import { academyGet, AcademyApiError } from "@/lib/academy/api";
import type { CourseDetail } from "@/lib/academy/types";

/** What a page shows to a visitor who is not signed in. */
export function SignInCard({ title = "Sign in to continue", text = "This page is for people who have signed in to the Academy." }: { title?: string; text?: string }) {
  const { signIn } = useAcademy();
  return (
    <section className="card panel state" data-testid="state-signin">
      <div style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--blue-2)", color: "var(--blue)", display: "grid", placeItems: "center", margin: "0 auto 14px" }}>
        <Icon name="user" size={26} />
      </div>
      <h2>{title}</h2>
      <p>{text}</p>
      <button type="button" className="btn primary" onClick={signIn}>Sign in</button>
    </section>
  );
}

/** A course's curriculum, for pages that need the order of its steps. */
export function useCourse(courseId: string) {
  const [course, setCourse] = useState<CourseDetail | null>(null);
  const [failed, setFailed] = useState<{ status: number } | null>(null);
  useEffect(() => {
    let cancelled = false;
    academyGet<CourseDetail>(`/public/courses/${courseId}`)
      .then((result) => { if (!cancelled) setCourse(result); })
      .catch((failure) => { if (!cancelled) setFailed({ status: failure instanceof AcademyApiError ? failure.status : 0 }); });
    return () => { cancelled = true; };
  }, [courseId]);
  return { course, failed };
}
