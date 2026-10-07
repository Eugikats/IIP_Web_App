"use client";

import Link from "next/link";
import { Award, Clock, GraduationCap, Users } from "lucide-react";
import { COURSES } from "@/lib/academy/courses";
import { downloadCertificate } from "@/lib/academy/certificate";
import { useAcademyProgress } from "@/lib/academy/progress";

// IIP Academy: short text courses taught against the real IIP screens. A person works through the steps,
// answers a few questions and receives a certificate of completion they can download.

export default function AcademyCatalogue() {
  const { completions } = useAcademyProgress();
  const done = (id: string) => completions.find((item) => item.courseId === id);

  return (
    <div className="cip-acq space-y-5">
      <div>
        <h1 className="m-0 inline-flex items-center gap-2 text-[20px] font-semibold tracking-tight text-[#0B1640]">
          <GraduationCap className="h-5 w-5 text-[#1769FF]" aria-hidden />
          IIP Academy
        </h1>
        <p className="m-0 mt-0.5 max-w-2xl text-[12.5px] text-slate-500">
          Short courses on how to use IIP well. Each one follows the real screens, ends with a few questions, and gives you a certificate of completion.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        {COURSES.map((course) => {
          const completion = done(course.id);
          return (
            <section key={course.id} className="flex flex-col rounded-xl border border-slate-200/90 bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.03)]">
              <div className="flex items-start justify-between gap-3">
                <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Course {course.number}</span>
                {completion ? (
                  <span className="inline-flex items-center gap-1 rounded-full border border-emerald-100 bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                    <Award className="h-3 w-3" aria-hidden />
                    Completed
                  </span>
                ) : null}
              </div>
              <h2 className="m-0 mt-1 text-[16px] font-bold leading-snug text-[#0B1640]">{course.title}</h2>
              <p className="m-0 mt-1.5 text-[13px] leading-relaxed text-slate-600">{course.short}</p>
              <dl className="m-0 mt-3 space-y-1.5 text-[12px] text-slate-500">
                <div className="flex gap-2"><Users className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden /><dd className="m-0">{course.audience}</dd></div>
                <div className="flex gap-2"><Clock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden /><dd className="m-0">{course.modules.length} steps, then a check. Full course: {course.duration}.</dd></div>
              </dl>
              <div className="mt-4 flex flex-wrap items-center gap-2 pt-1">
                <Link href={`/app/academy/${course.id}`} className="inline-flex h-9 items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6]">
                  {completion ? "Review course" : "Start course"}
                </Link>
                {completion ? (
                  <button
                    type="button"
                    onClick={() => void downloadCertificate({ name: completion.name, courseTitle: course.title, completedAt: completion.completedAt, certificateId: completion.certificateId })}
                    className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-semibold text-slate-700 hover:bg-slate-50"
                  >
                    <Award className="h-4 w-4 text-[#1769FF]" aria-hidden />
                    Download certificate
                  </button>
                ) : null}
              </div>
            </section>
          );
        })}
      </div>

      <section className="rounded-xl border border-slate-200/90 bg-white p-5">
        <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">How the courses fit together</h2>
        <p className="m-0 mt-1 text-[12.5px] text-slate-600">
          Governance is the foundation. The other three follow the life of a credit-linked policy: underwrite, claim, protect.
        </p>
        <ol className="m-0 mt-3 flex list-none flex-wrap items-center gap-2 p-0 text-[12px] font-semibold text-[#0B1640]">
          {["Governance", "Underwriting", "Claims", "Fraud and identity"].map((label, index) => (
            <li key={label} className="inline-flex items-center gap-2">
              <span className="rounded-full border border-[#1769FF]/25 bg-[#EEF4FF] px-3 py-1 text-[#1769FF]">{label}</span>
              {index < 3 ? <span className="text-slate-300" aria-hidden>&rarr;</span> : null}
            </li>
          ))}
        </ol>
        <p className="m-0 mt-3 text-[11.5px] text-slate-500">
          Every example uses made-up people and numbers. A certificate shows you finished a course. It is not an accredited qualification.
        </p>
      </section>
    </div>
  );
}
