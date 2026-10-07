"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft, ArrowRight, Award, Check, ChevronRight, Download, ImageOff } from "lucide-react";
import { toast } from "sonner";
import { getCourse, type Course, type Module } from "@/lib/academy/courses";
import { downloadCertificate } from "@/lib/academy/certificate";
import { useAcademyProgress } from "@/lib/academy/progress";

// A course as steps: each module is one page (goal, the idea as a flow, a small real screen from IIP, a
// worked example, an exercise, takeaways). After the last module come a few questions, then the
// certificate of completion.

export default function CoursePlayer() {
  const params = useParams<{ courseId: string }>();
  const course = getCourse(params?.courseId ?? "");
  if (!course) {
    return (
      <div className="cip-acq space-y-3">
        <p className="m-0 text-[14px] font-semibold text-[#0B1640]">This course was not found.</p>
        <Link href="/app/academy" className="text-[13px] font-semibold text-[#1769FF]">Back to IIP Academy</Link>
      </div>
    );
  }
  return <Player course={course} />;
}

function Player({ course }: { course: Course }) {
  const total = course.modules.length;
  const [index, setIndex] = useState(0); // 0..total-1 modules, total = check, total+1 = certificate
  const { completions, complete, displayName } = useAcademyProgress();
  const existing = completions.find((item) => item.courseId === course.id);
  const [name, setName] = useState("");
  const [answers, setAnswers] = useState<Record<number, number>>({});
  const [checked, setChecked] = useState(false);
  const [enlarged, setEnlarged] = useState<string | null>(null);

  useEffect(() => { if (!name) setName(existing?.name ?? displayName); }, [displayName, existing, name]);
  useEffect(() => { window.scrollTo({ top: 0 }); }, [index]);

  const allCorrect = course.quiz.every((question, i) => answers[i] === question.answer);
  const answeredAll = course.quiz.every((_, i) => answers[i] !== undefined);
  const stepLabels = useMemo(() => [...course.modules.map((m) => m.title), "Check your understanding", "Your certificate"], [course]);
  const onCertificate = index === total + 1;

  const finish = async () => {
    const entry = complete(course.id, name);
    setIndex(total + 1);
    toast.success("Course completed", { description: "Your certificate is ready to download." });
    return entry;
  };

  const certificate = existing ?? null;

  return (
    <div className="cip-acq space-y-4">
      <div>
        <Link href="/app/academy" className="inline-flex items-center gap-1 text-[12px] font-semibold text-slate-500 hover:text-[#1769FF]">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          IIP Academy
        </Link>
        <h1 className="m-0 mt-1 text-[20px] font-semibold tracking-tight text-[#0B1640]">{course.title}</h1>
        <p className="m-0 mt-0.5 max-w-3xl text-[12.5px] text-slate-500">{course.aim}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <nav aria-label="Course steps" className="lg:sticky lg:top-4 lg:self-start">
          <ol className="m-0 flex list-none gap-1.5 overflow-x-auto p-0 lg:flex-col lg:overflow-visible">
            {stepLabels.map((label, i) => {
              const reached = i <= index || !!existing;
              const current = i === index;
              return (
                <li key={label} className="shrink-0">
                  <button
                    type="button"
                    disabled={!reached}
                    onClick={() => setIndex(i)}
                    aria-current={current ? "step" : undefined}
                    className={`flex w-full items-center gap-2 rounded-lg border px-3 py-2 text-left text-[12px] font-medium ${
                      current ? "border-[#1769FF] bg-[#EEF4FF] text-[#1769FF]" : "border-slate-200 bg-white text-slate-600 hover:bg-slate-50"
                    } disabled:cursor-not-allowed disabled:opacity-50`}
                  >
                    <span className={`inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10.5px] font-bold ${i < index || (existing && i < total + 1) ? "bg-emerald-500 text-white" : current ? "bg-[#1769FF] text-white" : "bg-slate-100 text-slate-500"}`}>
                      {i < index || (existing && i < total + 1) ? <Check className="h-3 w-3" aria-hidden /> : i + 1}
                    </span>
                    <span className="whitespace-nowrap lg:whitespace-normal">{label}</span>
                  </button>
                </li>
              );
            })}
          </ol>
        </nav>

        <div className="min-w-0 space-y-4">
          <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" aria-hidden>
            <div className="h-full rounded-full bg-[#1769FF] transition-[width] duration-500" style={{ width: `${Math.round(((Math.min(index, total + 1) + (onCertificate ? 0 : 1)) / (total + 2)) * 100)}%` }} />
          </div>

          {index < total ? (
            <ModuleView module={course.modules[index]} number={index + 1} of={total} onEnlarge={setEnlarged} onBack={index > 0 ? () => setIndex(index - 1) : undefined} onNext={() => setIndex(index + 1)} nextLabel={index === total - 1 ? "Check your understanding" : "Next step"} />
          ) : index === total ? (
            <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
              <h2 className="m-0 text-[17px] font-bold text-[#0B1640]">Check your understanding</h2>
              <p className="m-0 mt-1 text-[12.5px] text-slate-500">Answer all {course.quiz.length} questions. You can try again until each one is right.</p>
              <ol className="m-0 mt-4 list-none space-y-5 p-0">
                {course.quiz.map((question, qi) => (
                  <li key={question.q}>
                    <p className="m-0 text-[14px] font-semibold text-[#0B1640]">{qi + 1}. {question.q}</p>
                    <div role="radiogroup" aria-label={question.q} className="mt-2 space-y-1.5">
                      {question.options.map((option, oi) => {
                        const chosen = answers[qi] === oi;
                        const right = checked && oi === question.answer;
                        const wrong = checked && chosen && oi !== question.answer;
                        return (
                          <label key={option} className={`flex cursor-pointer items-start gap-2 rounded-lg border px-3 py-2 text-[13px] ${right ? "border-emerald-300 bg-emerald-50" : wrong ? "border-red-200 bg-red-50" : chosen ? "border-[#1769FF] bg-[#EEF4FF]" : "border-slate-200 bg-white hover:bg-slate-50"}`}>
                            <input type="radio" name={`q${qi}`} checked={chosen} onChange={() => { setAnswers({ ...answers, [qi]: oi }); setChecked(false); }} className="mt-0.5 accent-[#1769FF]" />
                            <span className="text-slate-700">{option}</span>
                          </label>
                        );
                      })}
                    </div>
                    {checked ? (
                      <p className={`m-0 mt-1.5 text-[12px] ${answers[qi] === question.answer ? "text-emerald-700" : "text-red-700"}`}>
                        {answers[qi] === question.answer ? `Right. ${question.why}` : "Not quite. Think again, then check once more."}
                      </p>
                    ) : null}
                  </li>
                ))}
              </ol>
              <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
                <button type="button" onClick={() => setIndex(total - 1)} className="inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-[13px] font-medium text-slate-600 hover:bg-slate-50">
                  <ArrowLeft className="h-4 w-4" aria-hidden />
                  Back
                </button>
                {checked && allCorrect ? (
                  <button type="button" onClick={() => void finish()} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6]">
                    <Check className="h-4 w-4" aria-hidden />
                    Done
                  </button>
                ) : (
                  <button type="button" disabled={!answeredAll} onClick={() => setChecked(true)} className="inline-flex h-9 items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50">
                    Check answers
                  </button>
                )}
              </div>
            </section>
          ) : (
            <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
              <div className="flex items-center gap-3">
                <span className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-emerald-50 text-emerald-600"><Award className="h-6 w-6" aria-hidden /></span>
                <div>
                  <h2 className="m-0 text-[17px] font-bold text-[#0B1640]">Course completed</h2>
                  <p className="m-0 text-[12.5px] text-slate-500">{course.title}</p>
                </div>
              </div>
              <label className="mt-5 block max-w-md">
                <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">Name on the certificate</span>
                <input value={name} onChange={(event) => setName(event.target.value)} maxLength={80} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-[14px] text-[#0B1640] outline-none focus:border-[#1769FF]/50" />
              </label>
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  disabled={!name.trim() || !certificate}
                  onClick={() => {
                    const entry = complete(course.id, name);
                    void downloadCertificate({ name: entry.name, courseTitle: course.title, completedAt: entry.completedAt, certificateId: entry.certificateId });
                  }}
                  className="inline-flex h-10 items-center gap-2 rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Download className="h-4 w-4" aria-hidden />
                  Download certificate (PDF)
                </button>
                <Link href="/app/academy" className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-[13px] font-semibold text-slate-700 hover:bg-slate-50">Back to IIP Academy</Link>
              </div>
              <p className="m-0 mt-4 text-[11.5px] text-slate-500">
                This is a certificate of completion. It is kept in this browser against your sign-in, and it is not an accredited qualification.
              </p>
            </section>
          )}
        </div>
      </div>

      {enlarged ? (
        <div role="dialog" aria-label="Screen from IIP" onClick={() => setEnlarged(null)} className="fixed inset-0 z-50 flex cursor-zoom-out items-center justify-center bg-slate-900/70 p-4">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={enlarged} alt="Screen from IIP, enlarged" className="max-h-full max-w-full rounded-lg border border-white/20 bg-white" />
        </div>
      ) : null}
    </div>
  );
}

function ModuleView({ module, number, of, onEnlarge, onBack, onNext, nextLabel }: { module: Module; number: number; of: number; onEnlarge: (src: string) => void; onBack?: () => void; onNext: () => void; nextLabel: string }) {
  return (
    <article className="space-y-4">
      <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
        <p className="m-0 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Step {number} of {of}</p>
        <h2 className="m-0 mt-1 text-[18px] font-bold leading-snug text-[#0B1640]">{module.title}</h2>
        <p className="m-0 mt-1.5 text-[13.5px] text-slate-600">{module.goal}</p>
        <h3 className="m-0 mt-4 text-[12px] font-bold uppercase tracking-wide text-slate-400">You will be able to</h3>
        <ul className="m-0 mt-1.5 space-y-1 p-0">
          {module.outcomes.map((outcome) => (
            <li key={outcome} className="flex list-none gap-2 text-[13px] text-slate-700"><Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-500" aria-hidden />{outcome}</li>
          ))}
        </ul>
      </section>

      <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
        <h3 className="m-0 text-[12px] font-bold uppercase tracking-wide text-slate-400">The idea in one picture</h3>
        <ol className="m-0 mt-3 flex list-none flex-wrap items-center gap-2 p-0">
          {module.flow.map((node, i) => (
            <Fragment key={node}>
              <li className="rounded-lg border border-[#1769FF]/25 bg-[#EEF4FF] px-3 py-2 text-[12.5px] font-semibold text-[#0B1640]">
                <span className="mr-1.5 text-[#1769FF]">{i + 1}</span>
                {node}
              </li>
              {i < module.flow.length - 1 ? <ChevronRight className="h-4 w-4 text-slate-300" aria-hidden /> : null}
            </Fragment>
          ))}
        </ol>
        {module.flowNote ? <p className="m-0 mt-3 text-[12.5px] leading-relaxed text-slate-600">{module.flowNote}</p> : null}
      </section>

      <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
        <h3 className="m-0 text-[12px] font-bold uppercase tracking-wide text-slate-400">In IIP: where this lives</h3>
        {"file" in module.screen ? (
          <div className="mt-3 grid gap-4 md:grid-cols-[minmax(0,22rem)_minmax(0,1fr)]">
            <button type="button" onClick={() => onEnlarge(`/academy/${(module.screen as { file: string }).file}`)} className="group block cursor-zoom-in overflow-hidden rounded-lg border border-slate-200 bg-slate-50 text-left" aria-label="Enlarge this screen">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={`/academy/${module.screen.file}`} alt={module.screen.caption} className="block w-full" loading="lazy" />
            </button>
            <div>
              <p className="m-0 text-[13px] text-slate-700">{module.screen.caption}</p>
              <ol className="m-0 mt-3 list-none space-y-1.5 p-0">
                {module.screen.callouts.map((callout, i) => (
                  <li key={callout} className="flex items-center gap-2 text-[12.5px] text-slate-600">
                    <span className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[#1769FF] text-[10.5px] font-bold text-white">{i + 1}</span>
                    {callout}
                  </li>
                ))}
              </ol>
              <p className="m-0 mt-3 text-[11px] text-slate-400">Select the picture to enlarge it. Synthetic data only.</p>
            </div>
          </div>
        ) : (
          <div className="mt-3 flex items-start gap-3 rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4">
            <ImageOff className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" aria-hidden />
            <div>
              <p className="m-0 text-[13px] font-semibold text-[#0B1640]">Screen not yet available in IIP</p>
              <p className="m-0 mt-1 text-[12.5px] text-slate-600">{module.screen.missing}</p>
            </div>
          </div>
        )}
      </section>

      <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
        <h3 className="m-0 text-[12px] font-bold uppercase tracking-wide text-slate-400">Worked example: {module.example.title}</h3>
        <ol className="m-0 mt-3 list-none space-y-2 p-0">
          {module.example.steps.map((step, i) => (
            <li key={step} className="flex gap-2.5 text-[13px] text-slate-700">
              <span className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-slate-100 text-[10.5px] font-bold text-slate-600">{i + 1}</span>
              {step}
            </li>
          ))}
        </ol>
        <div className="mt-4 rounded-lg border border-[#1769FF]/20 bg-[#F5F8FF] p-3.5">
          <p className="m-0 text-[11px] font-bold uppercase tracking-wide text-[#1769FF]">Try it yourself</p>
          <p className="m-0 mt-1 text-[13px] text-slate-700">{module.tryIt}</p>
        </div>
      </section>

      <section className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6">
        <h3 className="m-0 text-[12px] font-bold uppercase tracking-wide text-slate-400">Key takeaways</h3>
        <ul className="m-0 mt-2 space-y-1.5 p-0">
          {module.takeaways.map((item) => <li key={item} className="list-none text-[13px] text-slate-700">{item}</li>)}
        </ul>
      </section>

      <div className="flex items-center justify-between gap-3">
        {onBack ? (
          <button type="button" onClick={onBack} className="inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-[13px] font-medium text-slate-600 hover:bg-slate-50">
            <ArrowLeft className="h-4 w-4" aria-hidden />
            Back
          </button>
        ) : <span />}
        <button type="button" onClick={onNext} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6]">
          {nextLabel}
          <ArrowRight className="h-4 w-4" aria-hidden />
        </button>
      </div>
    </article>
  );
}
