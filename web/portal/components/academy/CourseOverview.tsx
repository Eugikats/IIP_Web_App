"use client";

import Link from "next/link";
import { ArrowLeft, Award, BadgeCheck, BookOpen, Check, Clock, ListChecks, Users } from "lucide-react";
import { isReady, type Course } from "@/lib/academy/courses";
import { formatPrice, useAcademyPricing } from "@/lib/academy/pricing";

// The page before a course starts, laid out like an online course listing: what it is, how long it
// takes, what is in it, and what it costs. The price comes from Settings. Payment is not connected, so
// the page says nobody is charged.

export default function CourseOverview({ course, completed, onBegin }: { course: Course; completed: boolean; onBegin: () => void }) {
  const { prices, currency, loading, error } = useAcademyPricing();
  const amount = prices?.[course.id];
  // A course whose lessons are still being written shows its outline and cannot be started.
  const ready = isReady(course);
  const items = ready ? course.modules.map((module) => ({ id: module.id, title: module.title, goal: module.goal })) : (course.outline ?? []).map((entry) => ({ id: entry.title, ...entry }));
  const outcomes = course.outcomes ?? Array.from(new Set(course.modules.flatMap((module) => module.outcomes))).slice(0, 8);

  return (
    <div className="cip-acq space-y-4">
      <div>
        <Link href="/app/academy" className="inline-flex items-center gap-1 text-[12px] font-semibold text-slate-500 hover:text-[#1769FF]">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          IIP Academy
        </Link>
        <p className="m-0 mt-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Course {course.number}</p>
        <h1 className="m-0 mt-0.5 text-[22px] font-semibold leading-snug tracking-tight text-[#0B1640]">{course.title}</h1>
        <p className="m-0 mt-1.5 max-w-3xl text-[13.5px] leading-relaxed text-slate-600">{course.aim}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-4">
          <section className="rounded-xl border border-slate-200/90 bg-white p-5">
            <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">Course details</h2>
            <dl className="m-0 mt-3 grid gap-3 2xl:grid-cols-2">
              <Detail icon={Clock} label="Duration" value={`Full course: ${course.duration}`} note={ready ? `This online version: ${course.modules.length} short steps and a check` : "The lessons for this course are being written. The outline shows what it covers."} />
              <Detail icon={Users} label="Who it is for" value={course.audience} />
              <Detail icon={ListChecks} label="What you do" value={ready ? `${course.modules.length} steps, each with a real IIP screen, a worked example and an exercise` : `${items.length} planned lessons, then a check`} note={ready ? `Then ${course.quiz.length} questions` : undefined} />
              <Detail icon={BadgeCheck} label="What you get" value="A certificate of completion with your name, as a PDF" note="Not an accredited qualification" />
            </dl>
          </section>

          <section className="rounded-xl border border-slate-200/90 bg-white p-5">
            <h2 className="m-0 inline-flex items-center gap-2 text-[14px] font-bold text-[#0B1640]"><BookOpen className="h-4 w-4 text-[#1769FF]" aria-hidden />{ready ? "Course content" : "Course outline"}</h2>
            <ol className="m-0 mt-3 list-none space-y-3 p-0">
              {items.map((module, index) => (
                <li key={module.id} className="flex gap-3">
                  <span className="mt-0.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#EEF4FF] text-[11px] font-bold text-[#1769FF]">{index + 1}</span>
                  <div>
                    <p className="m-0 text-[13.5px] font-semibold text-[#0B1640]">{module.title}</p>
                    <p className="m-0 mt-0.5 text-[12.5px] text-slate-600">{module.goal}</p>
                  </div>
                </li>
              ))}
              {ready ? (
                <li className="flex gap-3">
                  <span className="mt-0.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-50 text-emerald-600"><Check className="h-3.5 w-3.5" aria-hidden /></span>
                  <div>
                    <p className="m-0 text-[13.5px] font-semibold text-[#0B1640]">Check your understanding</p>
                    <p className="m-0 mt-0.5 text-[12.5px] text-slate-600">{course.quiz.length} questions with the reason for each answer, then your certificate.</p>
                  </div>
                </li>
              ) : null}
            </ol>
          </section>

          <section className="rounded-xl border border-slate-200/90 bg-white p-5">
            <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">What you will be able to do</h2>
            <ul className="m-0 mt-2 space-y-1.5 p-0">
              {outcomes.map((outcome) => (
                <li key={outcome} className="flex list-none gap-2 text-[13px] text-slate-700"><Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-500" aria-hidden />{outcome}</li>
              ))}
            </ul>
          </section>
        </div>

        <aside className="lg:sticky lg:top-4 lg:self-start">
          <section className="rounded-xl border border-[#1769FF]/25 bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
            <p className="m-0 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Billing plan</p>
            <p className="m-0 mt-1 text-[30px] font-bold leading-none tracking-tight text-[#0B1640]">
              {loading ? <span className="inline-block h-7 w-20 animate-pulse rounded bg-slate-100" aria-label="Loading the price" /> : amount === undefined ? "Not available" : formatPrice(amount, currency)}
            </p>
            <p className="m-0 mt-1 text-[12.5px] text-slate-600">{amount === 0 ? "No charge for this course" : "One payment per learner, for this course"}</p>
            {error ? <p role="alert" className="m-0 mt-2 text-[12px] text-red-700">{error}</p> : null}
            {ready ? (
              <>
                <ul className="m-0 mt-3 space-y-1.5 p-0">
                  {["Every step and exercise", "The check with explained answers", "Your certificate of completion (PDF)", "Come back to review any time"].map((item) => (
                    <li key={item} className="flex list-none gap-2 text-[12.5px] text-slate-700"><Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-500" aria-hidden />{item}</li>
                  ))}
                </ul>
                <button type="button" onClick={onBegin} className="mt-4 inline-flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-[#1769FF] text-[13.5px] font-semibold text-white hover:bg-[#1258d6]">
                  {completed ? <Award className="h-4 w-4" aria-hidden /> : null}
                  {completed ? "Review course" : "Start course"}
                </button>
              </>
            ) : (
              <>
                <p data-testid="lessons-in-preparation" className="m-0 mt-3 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-[12.5px] leading-relaxed text-slate-700">
                  The lessons for this course are in preparation, so it cannot be started yet. The outline on this page is what it will cover.
                </p>
                <Link href="/app/academy" className="mt-4 inline-flex h-10 w-full items-center justify-center rounded-lg border border-slate-200 bg-white text-[13.5px] font-semibold text-slate-700 hover:bg-slate-50">
                  Back to all courses
                </Link>
              </>
            )}
            <p className="m-0 mt-3 text-[11.5px] leading-relaxed text-slate-500">Payment is not connected in this workspace yet, so nobody is charged. The price is set by your administrator in Settings.</p>
          </section>
        </aside>
      </div>
    </div>
  );
}

function Detail({ icon: Icon, label, value, note }: { icon: typeof Clock; label: string; value: string; note?: string }) {
  return (
    <div className="flex gap-2.5">
      <Icon className="mt-0.5 h-4 w-4 shrink-0 text-[#1769FF]" aria-hidden />
      <div>
        <dt className="text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">{label}</dt>
        <dd className="m-0 mt-0.5 text-[13px] text-slate-700">{value}</dd>
        {note ? <dd className="m-0 text-[11.5px] text-slate-500">{note}</dd> : null}
      </div>
    </div>
  );
}
