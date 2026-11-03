"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { Thumb } from "@/components/academy/Thumb";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { CheckoutDialog } from "@/components/academy/CheckoutDialog";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { academyGet, AcademyApiError } from "@/lib/academy/api";
import { categoryTitle } from "@/lib/academy/categoryTitles";
import { formatMoney } from "@/lib/academy/format";
import { ZENITH_READY } from "@/lib/academy/flags";
import { askZenith } from "@/lib/academy/zenithStore";
import type { CourseDetail } from "@/lib/academy/types";

// A course page. Not enrolled: the sales page, with the curriculum, who it is for and the price. Enrolled: the same page with
// the person's progress and the way back into the lesson they were on.
export default function CoursePage({ courseId }: { courseId: string }) {
  const { me, learnerReady, paymentsConfigured, isEnrolled, progressOf, hasCertificate, showModal, signIn, setZenithOpen } = useAcademy();
  const [course, setCourse] = useState<CourseDetail | null>(null);
  const [problem, setProblem] = useState<{ status: number; message: string } | null>(null);

  useEffect(() => {
    let cancelled = false;
    setCourse(null);
    setProblem(null);
    academyGet<CourseDetail>(`/public/courses/${courseId}`)
      .then((result) => { if (!cancelled) setCourse(result); })
      .catch((failure) => { if (!cancelled) setProblem({ status: failure instanceof AcademyApiError ? failure.status : 0, message: failure instanceof AcademyApiError ? failure.message : "The course could not be loaded." }); });
    return () => { cancelled = true; };
  }, [courseId]);

  const barVisible = Boolean(course) && course?.status !== "coming_soon" && course?.paidReleaseApproved !== false && !isEnrolled(courseId);
  useEffect(() => {
    document.getElementById("iipa")?.classList.toggle("hasbar", barVisible);
    return () => document.getElementById("iipa")?.classList.remove("hasbar");
  }, [barVisible]);

  if (problem) {
    return problem.status === 404
      ? <PageState kind="notfound" title="Page not found" text="That course does not exist." action={{ label: "All courses", href: "/academy/courses" }} />
      : <PageState kind="error" title="The course could not be loaded" text={problem.message} />;
  }
  if (!course || !learnerReady) return <SkeletonCards count={1} />;

  const soon = course.status === "coming_soon";
  const enrolled = isEnrolled(course.id);
  const progress = progressOf(course.id);
  const done = new Set(progress.doneLessons);
  const allLessons = course.modules.flatMap((module) => module.lessons);
  const total = allLessons.length;
  const percent = total ? Math.round((done.size / total) * 100) : 0;
  const nextLesson = allLessons.find((lesson) => !done.has(lesson.id)) ?? allLessons[0];
  const certified = hasCertificate(course.id);
  const signedIn = me?.signedIn === true;

  const enrol = () => {
    if (!signedIn) { signIn(); return; }
    showModal(<CheckoutDialog course={{ id: course.id, title: course.title, hue: course.hue, icon: course.icon, lessons: total, price: course.price, currency: course.currency }} />, "left");
  };

  const included: [string, string][] = [
    ["video", `${total} lessons`],
    ["layers", `${course.modules.length} modules`],
    ["award", "Certificate of completion"],
    ...(ZENITH_READY ? ([["sparkle", "Zenith AI tutor on every lesson"]] as [string, string][]) : []),
    ["users", "Community access"],
    ["infinity", "Learn at your own pace"],
  ];

  const status = soon ? "Coming soon" : certified ? "Completed" : enrolled ? (percent > 0 ? "In progress" : "Not started") : null;

  const priceCard = soon ? (
    <section className="card pricecard">
      <div className="tagline">Coming soon</div>
      <p className="pnote">The lessons for this course are being prepared. The outline on this page shows what it will cover.</p>
      <button type="button" className="btn primary block" disabled>Coming soon</button>
    </section>
  ) : enrolled ? (
    <section className="card pricecard" data-testid="enrolled-card">
      <div className="tagline"><Icon name="checkc" size={16} />You&apos;re enrolled</div>
      <div className="bigpct">{percent}%</div>
      <div className="bar"><i style={{ width: `${percent}%` }} /></div>
      <p className="pnote">
        {done.size} of {total} lessons completed{certified ? ". Well done!" : nextLesson ? <>. Next: <b>{nextLesson.title}</b></> : "."}
      </p>
      {certified ? (
        <Link className="btn primary block" href="/academy/certificates">View certificate <Icon name="award" size={16} /></Link>
      ) : (
        <Link className="btn primary block" href={`/academy/learn/${course.id}/${nextLesson?.id ?? ""}`} data-testid="continue-course">{percent ? "Continue learning" : "Start course"} <Icon name="arrowR" size={16} /></Link>
      )}
      {ZENITH_READY && signedIn ? <button type="button" className="btn ghost block" style={{ marginTop: 10 }} onClick={() => { setZenithOpen(true); void askZenith(`Summarise ${course.title}`); }}><Icon name="sparkle" size={16} />Ask Zenith about this course</button> : null}
    </section>
  ) : course.paidReleaseApproved === false ? (
    <section className="card pricecard" data-testid="reviewer-edition">
      <div className="tagline">Reviewer edition</div>
      <div className="price">{formatMoney(course.price, course.currency)}</div>
      <p className="pnote">Planned release price. Paid enrolment is blocked while content, assessment and audio reviews are pending. An Academy operator can grant reviewer access.</p>
    </section>
  ) : (
    <section className="card pricecard">
      <div className="tagline">Full course access</div>
      <div className="price">{formatMoney(course.price, course.currency)}</div>
      <p className="pnote">
        {paymentsConfigured
          ? (signedIn ? "Pay by card or mobile money. Tax is included in the price where it applies." : "Sign in to enrol. You can pay by card or mobile money.")
          : (signedIn ? "Payment is not connected yet, so enrolment is open and nothing is charged." : "Sign in to enrol. Payment is not connected yet, so nothing is charged.")}
      </p>
      <button type="button" className="btn primary block" onClick={enrol} data-testid="enrol">{signedIn ? "Enrol now" : "Sign in to enrol"} <Icon name="arrowR" size={16} /></button>
      {ZENITH_READY && signedIn ? <button type="button" className="btn ghost block" style={{ marginTop: 10 }} onClick={() => { setZenithOpen(true); void askZenith(`Summarise ${course.title}`); }}><Icon name="sparkle" size={16} />Ask Zenith about this course</button> : null}
      <div className="incl">{included.map(([icon, text]) => <div key={text}><Icon name={icon} size={18} /><span>{text}</span></div>)}</div>
    </section>
  );

  return (
    <>
      <div className="crumbs"><Link href="/academy/courses">All courses</Link><Icon name="chevR" size={14} /><span>{course.title}</span></div>
      <section className="card shero">
        <div>
          <div className="cat">{categoryTitle(course.category)}</div>
          <h1>{course.title}</h1>
          <p className="d">{course.description}</p>
          <div className="cmeta">
            {soon ? <span><Icon name="clock" size={16} />Coming soon</span> : (
              <>
                <span><Icon name="layers" size={16} />{course.modules.length} modules</span>
                <span><Icon name="video" size={16} />{total} lessons</span>
                {course.level ? <span><Icon name="target" size={16} />{course.level}</span> : null}
                <span><Icon name="award" size={16} />Certificate</span>
              </>
            )}
          </div>
        </div>
        <Thumb course={course} badge={status} />
      </section>

      {course.contentStatus && course.id !== "ai-agentic-insurance" && course.id !== "ai-sales-distribution" ? (
        <div className="notice" style={{ marginTop: 18 }} data-testid="content-status"><Icon name="bulb" size={18} /><span>{course.contentStatus}</span></div>
      ) : null}

      <div className="sale">
        <div className="salemain">
          {course.outcomes.length ? (
            <section className="card panel">
              <h2>What you&apos;ll learn</h2>
              <div className="outs">{course.outcomes.map((outcome) => <div key={outcome}><Icon name="checkc" size={18} /><span>{outcome}</span></div>)}</div>
            </section>
          ) : null}
          {course.who.length ? (
            <section className="card panel">
              <h2>Who this course is for</h2>
              <div className="who">{course.who.map((entry) => <div key={entry}><Icon name="user" size={18} /><span>{entry}</span></div>)}</div>
            </section>
          ) : null}

          <section className="card panel">
            <h2>{soon ? "What it will cover" : "Course content"}</h2>
            {soon ? (
              <>
                <p className="pnote" style={{ margin: "-8px 0 10px" }}>{course.outline.length} units planned</p>
                {Array.from(new Set(course.outline.map((item) => item.module ?? ""))).map((moduleName, index) => (
                  <div className="cmod" key={moduleName || index}>
                    <h4><span className="modn">{index + 1}</span>{moduleName || "Units"}</h4>
                    <ol>{course.outline.filter((item) => (item.module ?? "") === moduleName).map((item) => <li key={item.unit}><i />{item.unit}</li>)}</ol>
                  </div>
                ))}
              </>
            ) : (
              <>
                <p className="pnote" style={{ margin: "-8px 0 10px" }}>{course.modules.length} modules · {total} lessons</p>
                {course.modules.map((module, index) => {
                  const all = enrolled && module.lessons.every((lesson) => done.has(lesson.id));
                  const record = module.assessment ? progress.assessments[module.assessment.id] : undefined;
                  return (
                    <div className="cmod" key={module.id}>
                      <h4><span className={`modn${all ? " done" : ""}`}>{all ? <Icon name="check" size={16} /> : index + 1}</span>{module.title}</h4>
                      <ol>
                        {module.lessons.map((lesson) => (
                          enrolled
                            ? <li key={lesson.id} className={done.has(lesson.id) ? "done" : ""}><Link href={`/academy/learn/${course.id}/${lesson.id}`} style={{ display: "flex", gap: 10, alignItems: "center", width: "100%" }}>{done.has(lesson.id) ? <Icon name="check" size={15} /> : <i />}{lesson.title}</Link></li>
                            : <li key={lesson.id}><i />{lesson.title}</li>
                        ))}
                        {module.assessment ? (
                          <li key={module.assessment.id} className={record?.passed ? "done" : ""}>
                            {record?.passed ? <Icon name="check" size={15} /> : <i />}
                            {enrolled ? <Link href={`/academy/assessment/${course.id}/${module.assessment.id}`}>{module.assessment.title} ({module.assessment.questions} questions)</Link> : <>{module.assessment.title} ({module.assessment.questions} questions)</>}
                          </li>
                        ) : null}
                      </ol>
                    </div>
                  );
                })}
                {course.check ? (
                  <div className="cmod">
                    <h4><span className={`modn${progress.checkPassed ? " done" : ""}`}>{progress.checkPassed ? <Icon name="check" size={16} /> : <Icon name="target" size={16} />}</span>{course.check.title}</h4>
                    <ol><li className={progress.checkPassed ? "done" : ""}><i />{enrolled ? <Link href={`/academy/assessment/${course.id}/${course.check.id}`}>{course.check.questions} questions, pass mark {course.check.passMark}%</Link> : `${course.check.questions} questions, pass mark ${course.check.passMark}%`}</li></ol>
                  </div>
                ) : null}
                {course.finalTask ? (
                  <div className="cmod">
                    <h4><span className={`modn${progress.finalTask.status === "passed" ? " done" : ""}`}>{progress.finalTask.status === "passed" ? <Icon name="check" size={16} /> : <Icon name="pen" size={16} />}</span>Final applied task</h4>
                    <ol><li className={progress.finalTask.status === "passed" ? "done" : ""}><i />{enrolled ? <Link href={`/academy/final/${course.id}`}>{course.finalTask.title}</Link> : course.finalTask.title}</li></ol>
                  </div>
                ) : null}
              </>
            )}
          </section>

          {course.requirements ? (
            <section className="card panel">
              <h2>To earn the certificate</h2>
              <p style={{ color: "#37415f", lineHeight: 1.7 }}>{course.requirements}</p>
            </section>
          ) : null}

          {course.sources.length ? (
            <section className="card panel">
              <h2>Sources</h2>
              <div className="who">{course.sources.map((source) => <div key={source.id}><Icon name="book" size={18} /><span><b>{source.id}</b> {source.title}{source.date ? `, ${source.date}` : ""}</span></div>)}</div>
            </section>
          ) : null}
        </div>
        <aside>{priceCard}</aside>
      </div>

      {!soon && !enrolled && course.paidReleaseApproved !== false ? (
        <div className="mbar"><div><small>Full course access</small><b>{formatMoney(course.price, course.currency)}</b></div><button type="button" className="btn primary" onClick={enrol}>{signedIn ? "Enrol now" : "Sign in to enrol"}</button></div>
      ) : null}
    </>
  );
}
