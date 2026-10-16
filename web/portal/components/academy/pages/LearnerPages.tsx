"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { Avatar } from "@/components/academy/Avatar";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { academyGet, academyPost, academyPut, AcademyApiError } from "@/lib/academy/api";
import { categoryTitle } from "@/lib/academy/categoryTitles";
import { timeAgo, todayLabel } from "@/lib/academy/format";
import type { CertificateView, LearnerState } from "@/lib/academy/types";

// The learner's own pages: profile, progress, certificates, notifications and settings. They share one gate: a visitor is
// asked to sign in, and a signed-in person waits for their state to load before anything is drawn.

function useLearnerGate() {
  const { me, loadingMe, learner, loadingLearner } = useAcademy();
  if (loadingMe || (me?.signedIn && !learner && loadingLearner)) return { state: "loading" as const };
  if (!me?.signedIn) return { state: "signed-out" as const };
  if (!learner) return { state: "error" as const };
  return { state: "ready" as const, learner, name: learner.profile.name || me.name, email: me.email };
}

function Gate({ gate, title, children }: { gate: ReturnType<typeof useLearnerGate>; title: string; children: (ready: { learner: LearnerState; name: string; email: string | null }) => React.ReactNode }) {
  if (gate.state === "loading") return <SkeletonCards count={2} />;
  if (gate.state === "signed-out") return <SignInCard title={`Sign in to see ${title}`} text="This page shows your own learning, so it needs you to sign in." />;
  if (gate.state === "error") return <PageState kind="error" title="Your learning could not be loaded" text="Try again in a moment." />;
  return <>{children({ learner: gate.learner, name: gate.name, email: gate.email })}</>;
}

export function ProfilePage() {
  const gate = useLearnerGate();
  const { courses, courseById } = useAcademy();
  return (
    <Gate gate={gate} title="your profile">
      {({ learner, name }) => {
        const profile = learner.profile;
        const mine = (courses ?? []).filter((course) => learner.enrolled.some((entry) => entry.courseId === course.id));
        const inProgress = mine.filter((course) => !learner.certificates.some((entry) => entry.courseId === course.id)).length;
        const lessonsDone = Object.values(learner.progress).reduce((sum, entry) => sum + entry.doneLessons.length, 0);
        const skills = Array.from(new Set(mine.filter((course) => (learner.progress[course.id]?.doneLessons.length ?? 0) > 0).map((course) => categoryTitle(course.category))));
        const line = [profile.jobTitle, profile.organisation].filter(Boolean).join(", ");
        return (
          <>
            <section className="card">
              <div className="pcover" />
              <div className="pmain">
                <Avatar name={name} size={104} />
                <div>
                  <h1>{name}</h1>
                  <p>{line || "Add your job title and organisation in Settings."}</p>
                </div>
                <Link className="btn ghost" href="/academy/settings" data-testid="edit-profile"><Icon name="pen" size={16} />Edit profile</Link>
              </div>
            </section>
            <div className="kpis">
              {([[inProgress, "Courses in progress"], [lessonsDone, "Lessons completed"], [learner.certificates.length, "Certificates"], [`${learner.stats.streakDays} ${learner.stats.streakDays === 1 ? "day" : "days"}`, "Learning streak"]] as const).map(([value, label]) => (
                <div key={label} className="card kpi"><b>{value}</b><span>{label}</span></div>
              ))}
            </div>
            <div className="cgrid2">
              <div>
                <section className="card panel">
                  <h2>About</h2>
                  {profile.about ? <p style={{ color: "#37415f", lineHeight: 1.7 }}>{profile.about}</p> : <p style={{ color: "var(--muted)" }}>Nothing here yet. <Link href="/academy/settings" style={{ color: "var(--blue)", fontWeight: 600 }}>Tell people about yourself</Link>.</p>}
                </section>
                <section className="card panel" style={{ marginTop: 18 }}>
                  <h2>Recent activity</h2>
                  {learner.activity.length ? learner.activity.slice(0, 6).map((entry, index) => (
                    <div key={index} className="srow">
                      <div className="gi" style={{ background: "var(--blue-2)", color: "var(--blue)" }}><Icon name="check" size={16} /></div>
                      <div><b>{entry.text}</b><small>{timeAgo(entry.at)}</small></div>
                    </div>
                  )) : <p style={{ color: "var(--muted)" }}>Your lessons and results will appear here.</p>}
                </section>
              </div>
              <aside>
                <section className="card panel">
                  <h2>Skills</h2>
                  {skills.length ? <div className="skills">{skills.map((skill) => <span key={skill}>{skill}</span>)}</div> : <p style={{ color: "var(--muted)" }}>Skills appear as you complete lessons.</p>}
                </section>
                <section className="card panel" style={{ marginTop: 18 }}>
                  <h2>Certificates</h2>
                  {learner.certificates.length ? learner.certificates.map((certificate) => (
                    <Link key={certificate.id} className="srow" href={`/academy/certificates/${certificate.id}`}>
                      <div className="gi" style={{ background: "var(--orange-2)", color: "var(--orange)" }}><Icon name="award" size={18} /></div>
                      <div><b>{courseById(certificate.courseId)?.title ?? certificate.courseTitle}</b><small>View certificate</small></div>
                    </Link>
                  )) : <p style={{ color: "var(--muted)" }}>None yet.</p>}
                </section>
              </aside>
            </div>
          </>
        );
      }}
    </Gate>
  );
}

export function ProgressPage() {
  const gate = useLearnerGate();
  const { courses, nextLessonId } = useAcademy();
  const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
  return (
    <Gate gate={gate} title="your progress">
      {({ learner }) => {
        const mine = (courses ?? []).filter((course) => learner.enrolled.some((entry) => entry.courseId === course.id));
        const done = (id: string) => learner.progress[id]?.doneLessons.length ?? 0;
        const total = mine.reduce((sum, course) => sum + course.lessons, 0);
        const lessonsDone = mine.reduce((sum, course) => sum + done(course.id), 0);
        const overall = total ? Math.round((lessonsDone / total) * 100) : 0;
        const stats = learner.stats;
        const week = stats.minutesWeek.reduce((sum, value) => sum + value, 0);
        const peak = Math.max(60, ...stats.minutesWeek);
        const goal = Number(learner.profile.goal) || 30;
        const goalPercent = Math.min(100, Math.round((stats.minutesToday / goal) * 100));
        return (
          <>
            <div className="pagehead"><h1>My progress</h1><p>Your learning across all enrolled courses.</p></div>
            <div className="kpis">
              {([[`${overall}%`, "Overall progress"], [lessonsDone, "Lessons completed"], [`${week} min`, "Learning this week"], [`${stats.streakDays} ${stats.streakDays === 1 ? "day" : "days"}`, "Current streak"]] as const).map(([value, label]) => (
                <div key={label} className="card kpi"><b>{value}</b><span>{label}</span></div>
              ))}
            </div>
            <div className="cgrid2">
              <section className="card panel">
                <h2>This week</h2>
                <div className="bars">
                  {stats.minutesWeek.map((minutes, index) => (
                    <div key={index}><i className={index === stats.todayIndex ? "today" : ""} style={{ height: `${(minutes / peak) * 100}%` }} title={`${minutes} min`} /><small>{DAYS[index]}</small></div>
                  ))}
                </div>
                <p style={{ color: "var(--muted)", fontSize: 13.5, marginTop: 12 }}>Daily goal: {goal} minutes. {DAYS[stats.todayIndex]} is today.</p>
              </section>
              <section className="card panel">
                <h2>Goal</h2>
                <div className="bigpct">{goalPercent}%</div>
                <div className="bar"><i style={{ width: `${goalPercent}%`, background: "var(--orange)" }} /></div>
                <p style={{ color: "var(--muted)", fontSize: 14 }}>{stats.minutesToday} of {goal} minutes today. Complete a lesson to add time.</p>
                <Link className="btn soft block" style={{ marginTop: 14 }} href="/academy/settings">Change daily goal</Link>
              </section>
            </div>
            <section className="card panel" style={{ marginTop: 18 }}>
              <h2>Courses</h2>
              {mine.length ? mine.map((course) => {
                const percent = course.lessons ? Math.round((done(course.id) / course.lessons) * 100) : 0;
                const certified = learner.certificates.find((entry) => entry.courseId === course.id);
                const next = nextLessonId(course);
                return (
                  <div key={course.id} className="prow">
                    <div className="mini" style={{ background: `linear-gradient(135deg,${course.hue[0]},${course.hue[1]})` }}><Icon name={course.icon} size={22} /></div>
                    <div className="grow">
                      <b>{course.title}</b>
                      <div className="pc" style={{ fontSize: 13 }}>{done(course.id)}/{course.lessons} lessons, {percent}%</div>
                      <div className="bar"><i style={{ width: `${percent}%` }} /></div>
                    </div>
                    <Link className="btn soft sm" href={certified ? `/academy/certificates/${certified.id}` : `/academy/learn/${course.id}${next ? `/${next}` : ""}`}>{certified ? "Certificate" : "Continue"}</Link>
                  </div>
                );
              }) : (
                <p style={{ color: "var(--muted)" }}>You have not enrolled in a course yet. <Link href="/academy/courses" style={{ color: "var(--blue)", fontWeight: 600 }}>Browse the courses</Link>.</p>
              )}
            </section>
          </>
        );
      }}
    </Gate>
  );
}

export function CertificatesPage() {
  const gate = useLearnerGate();
  const { courses, nextLessonId } = useAcademy();
  return (
    <Gate gate={gate} title="your certificates">
      {({ learner }) => {
        const pending = (courses ?? []).filter((course) => learner.enrolled.some((entry) => entry.courseId === course.id) && !learner.certificates.some((entry) => entry.courseId === course.id));
        return (
          <>
            <div className="pagehead"><h1>Certificates</h1><p>{learner.certificates.length} earned, {pending.length} in progress.</p></div>
            {learner.certificates.length === 0 && pending.length === 0 ? (
              <PageState kind="empty" title="No certificates yet" text="Enrol in a course and finish it to earn your first certificate." action={{ label: "Browse courses", href: "/academy/courses" }} />
            ) : (
              <div className="grid3">
                {learner.certificates.map((certificate) => (
                  <div key={certificate.id} className="card certcard" data-testid="certificate-card">
                    <div className="cico"><Icon name="award" size={28} /></div>
                    <h3>{certificate.courseTitle}</h3>
                    <small>Issued {todayLabel(certificate.issuedAt)} · {certificate.code}</small>
                    <Link className="btn primary sm" href={`/academy/certificates/${certificate.id}`}>View certificate</Link>
                  </div>
                ))}
                {pending.map((course) => {
                  const percent = course.lessons ? Math.round(((learner.progress[course.id]?.doneLessons.length ?? 0) / course.lessons) * 100) : 0;
                  const next = nextLessonId(course);
                  return (
                    <div key={course.id} className="card certcard locked">
                      <div className="cico"><Icon name="lock" size={26} /></div>
                      <h3>{course.title}</h3>
                      <small>{percent}% complete, finish the course to unlock</small>
                      <div className="bar" style={{ margin: 0 }}><i style={{ width: `${percent}%` }} /></div>
                      <Link className="btn soft sm" href={`/academy/learn/${course.id}${next ? `/${next}` : ""}`}>Keep learning</Link>
                    </div>
                  );
                })}
              </div>
            )}
          </>
        );
      }}
    </Gate>
  );
}

export function CertificatePage({ certificateId }: { certificateId: string }) {
  const { me, loadingMe, courseById, toast } = useAcademy();
  const [certificate, setCertificate] = useState<CertificateView | null>(null);
  const [problem, setProblem] = useState<number | null>(null);

  useEffect(() => {
    if (!me?.signedIn) return;
    let cancelled = false;
    academyGet<CertificateView>(`/learner/certificates/${certificateId}`)
      .then((result) => { if (!cancelled) setCertificate(result); })
      .catch((failure) => { if (!cancelled) setProblem(failure instanceof AcademyApiError ? failure.status : 0); });
    return () => { cancelled = true; };
  }, [me, certificateId]);

  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to see your certificate" />;
  if (problem === 404) return <PageState kind="notfound" title="Certificate not found" text="This certificate is not on your account." action={{ label: "All certificates", href: "/academy/certificates" }} />;
  if (problem !== null) return <PageState kind="error" title="The certificate could not be loaded" />;
  if (!certificate) return <SkeletonCards count={1} />;

  const course = courseById(certificate.courseId);
  const verifyUrl = `${typeof window === "undefined" ? "" : window.location.origin}/academy/verify/${certificate.code}`;
  const share = async () => {
    try {
      await navigator.clipboard.writeText(verifyUrl);
      toast("Verification link copied", "share");
    } catch {
      toast(verifyUrl);
    }
  };

  return (
    <>
      <div className="crumbs"><Link href="/academy/certificates">Certificates</Link><Icon name="chevR" size={14} /><span>{certificate.courseTitle}</span></div>
      <div className="certificate" data-testid="certificate">
        <div className="inner">
          <div className="seal"><Icon name="award" size={40} /></div>
          <div className="ct">IIP Academy</div>
          <h2>Certificate of Completion</h2>
          <p style={{ color: "var(--muted)" }}>This certifies that</p>
          <div className="nm">{certificate.name}</div>
          <p style={{ color: "var(--muted)" }}>has successfully completed the course</p>
          <div className="cname">{certificate.courseTitle}</div>
          <p style={{ color: "var(--muted)", fontSize: 13, marginTop: 14 }}>
            {course ? `${course.lessons} lessons · ` : ""}Issued {todayLabel(certificate.issuedAt)} · Credential {certificate.code}
          </p>
          <p style={{ color: "var(--muted)", fontSize: 12.5, marginTop: 10 }}>Check this certificate at {verifyUrl.replace(/^https?:\/\//, "")}</p>
        </div>
      </div>
      <div className="noprint" style={{ display: "flex", gap: 10, justifyContent: "center", marginTop: 20, flexWrap: "wrap" }}>
        <button type="button" className="btn primary" onClick={() => window.print()}><Icon name="download" size={16} />Download PDF</button>
        <button type="button" className="btn ghost" onClick={() => void share()}><Icon name="share" size={16} />Share credential</button>
        <Link className="btn ghost" href="/academy/certificates">All certificates</Link>
      </div>
    </>
  );
}

export function VerifyPage({ code }: { code: string | null }) {
  const [entered, setEntered] = useState(code ?? "");
  const [result, setResult] = useState<{ valid: boolean; name?: string; courseTitle?: string; issuedAt?: string; code?: string; message?: string } | null>(null);
  const [checking, setChecking] = useState(false);

  const check = async (value: string) => {
    setChecking(true);
    setResult(null);
    try {
      setResult(await academyGet(`/public/certificates/${encodeURIComponent(value.trim())}`));
    } catch (failure) {
      setResult({ valid: false, message: failure instanceof AcademyApiError && failure.status === 404 ? "No certificate has this code." : "The certificate could not be checked. Try again." });
    } finally {
      setChecking(false);
    }
  };

  useEffect(() => {
    if (code) void check(code);
    // Only the code in the address starts a check on its own.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [code]);

  return (
    <div className="signwrap">
      <div className="card signcard" style={{ textAlign: "center" }}>
        <h1>Check a certificate</h1>
        <p>Enter the credential code printed on an IIP Academy certificate.</p>
        <form onSubmit={(event) => { event.preventDefault(); if (entered.trim()) void check(entered); }}>
          <label className="field" style={{ textAlign: "left" }}>
            Credential code
            <input value={entered} onChange={(event) => setEntered(event.target.value)} placeholder="IIPA-XXXX-XXXX-XXXX" autoComplete="off" />
          </label>
          <button className="btn primary block" type="submit" disabled={checking || !entered.trim()}>{checking ? "Checking..." : "Check certificate"}</button>
        </form>
        {result ? (
          result.valid ? (
            <div className="notice blue" style={{ marginTop: 16, textAlign: "left" }} data-testid="verify-valid">
              <Icon name="checkc" size={18} />
              <span><b>This certificate is genuine.</b><br />{result.name} completed {result.courseTitle}. Issued {result.issuedAt ? todayLabel(result.issuedAt) : ""}. Credential {result.code}.</span>
            </div>
          ) : (
            <div className="notice" style={{ marginTop: 16, textAlign: "left" }} role="alert" data-testid="verify-invalid">
              <Icon name="x" size={18} /><span>{result.message ?? "No certificate has this code."}</span>
            </div>
          )
        ) : null}
        <p style={{ fontSize: 12.5, margin: "16px 0 0" }}><Link href="/academy" style={{ color: "var(--blue)", fontWeight: 600 }}>Back to IIP Academy</Link></p>
      </div>
    </div>
  );
}

export function NotificationsPage() {
  const gate = useLearnerGate();
  const { refreshLearner, toast } = useAcademy();
  const markRead = async (id: string | null) => {
    try {
      await academyPost("/learner/notifications/read", { id });
      await refreshLearner();
    } catch {
      toast("That could not be saved", "x");
    }
  };
  return (
    <Gate gate={gate} title="your notifications">
      {({ learner }) => {
        const unread = learner.notifications.filter((entry) => !entry.read).length;
        return (
          <>
            <div className="pagehead" style={{ display: "flex", alignItems: "flex-end", gap: 14, flexWrap: "wrap" }}>
              <div><h1>Notifications</h1><p>{unread} unread</p></div>
              {unread ? <button type="button" className="btn ghost sm" style={{ marginLeft: "auto" }} onClick={() => void markRead(null)} data-testid="read-all"><Icon name="check" size={15} />Mark all as read</button> : null}
            </div>
            {learner.notifications.length ? (
              <section className="card">
                {learner.notifications.map((entry) => (
                  <Link key={entry.id} href={entry.href || "/academy"} className={`nrow${entry.read ? " read" : ""}`} onClick={() => { if (!entry.read) void markRead(entry.id); }}>
                    <div className="ni"><Icon name={entry.icon || "bell"} size={20} /></div>
                    <div><b>{entry.text}</b><small>{timeAgo(entry.createdAt)}</small></div>
                    {entry.read ? null : <span className="ud" />}
                  </Link>
                ))}
              </section>
            ) : (
              <PageState kind="empty" title="Nothing yet" text="Updates about your courses, results and certificates will appear here." />
            )}
          </>
        );
      }}
    </Gate>
  );
}

export function SettingsPage() {
  const gate = useLearnerGate();
  const { setLearner, toast } = useAcademy();
  return (
    <Gate gate={gate} title="your settings">
      {({ learner, email }) => <SettingsForm initial={learner.profile} email={email} onSaved={(next) => { setLearner(next); toast("Settings saved"); }} />}
    </Gate>
  );
}

function SettingsForm({ initial, email, onSaved }: { initial: LearnerState["profile"]; email: string | null; onSaved: (state: LearnerState) => void }) {
  const [form, setForm] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((current) => ({ ...current, [key]: value }));

  const save = async () => {
    setError(null);
    if (form.name.trim().length < 2) { setError("Enter your name, at least 2 characters."); return; }
    setBusy(true);
    try {
      onSaved(await academyPut<LearnerState>("/learner/profile", form));
    } catch (failure) {
      setError(failure instanceof AcademyApiError ? failure.message : "Your settings could not be saved.");
    } finally {
      setBusy(false);
    }
  };

  const toggle = (key: "emailNotifications" | "weeklySummary" | "replies" | "aiSuggestions", title: string, hint: string) => (
    <div className="setrow">
      <div><b>{title}</b><small>{hint}</small></div>
      <label className="tog"><input type="checkbox" checked={form[key]} onChange={(event) => set(key, event.target.checked)} aria-label={title} /><i /></label>
    </div>
  );

  return (
    <>
      <div className="pagehead"><h1>Settings</h1><p>Manage your profile, notifications and learning preferences.</p></div>
      <section className="card panel">
        <h2>Profile</h2>
        <div className="setrow"><div><b>Full name</b><small>Shown on certificates</small></div><input type="text" value={form.name} onChange={(event) => set("name", event.target.value)} aria-label="Full name" maxLength={80} /></div>
        <div className="setrow"><div><b>Email</b><small>Used to sign in. It comes from your account</small></div><input type="email" value={email ?? ""} readOnly aria-label="Email" placeholder="Not shown by your sign-in" /></div>
        <div className="setrow"><div><b>Job title</b></div><input type="text" value={form.jobTitle} onChange={(event) => set("jobTitle", event.target.value)} aria-label="Job title" maxLength={100} /></div>
        <div className="setrow"><div><b>Organisation</b></div><input type="text" value={form.organisation} onChange={(event) => set("organisation", event.target.value)} aria-label="Organisation" maxLength={100} /></div>
        <div className="setrow"><div><b>About you</b><small>Shown on your profile</small></div><textarea value={form.about} onChange={(event) => set("about", event.target.value)} aria-label="About you" maxLength={600} rows={3} /></div>
      </section>
      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>Notifications</h2>
        {toggle("emailNotifications", "Email notifications", "Course updates and new modules")}
        {toggle("weeklySummary", "Weekly progress summary", "Every Monday morning")}
        {toggle("replies", "Community replies", "When someone replies to you")}
        {toggle("aiSuggestions", "AI assistant suggestions", "Personalised study nudges")}
      </section>
      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>Learning</h2>
        <div className="setrow">
          <div><b>Daily goal</b><small>Minutes of learning per day</small></div>
          <select value={form.goal} onChange={(event) => set("goal", event.target.value)} aria-label="Daily goal">
            {["15", "30", "45", "60"].map((goal) => <option key={goal} value={goal}>{goal} minutes</option>)}
          </select>
        </div>
      </section>
      {error ? <div className="notice" role="alert" style={{ marginTop: 18 }}><Icon name="x" size={18} /><span>{error}</span></div> : null}
      <div style={{ display: "flex", gap: 10, marginTop: 18, flexWrap: "wrap" }}>
        <button type="button" className="btn primary" disabled={busy} onClick={() => void save()} data-testid="save-settings">{busy ? "Saving..." : "Save changes"}</button>
      </div>
    </>
  );
}

export function SignedOutPage() {
  const { signIn, me } = useAcademy();
  return (
    <div className="signwrap">
      <div className="card signcard" style={{ textAlign: "center" }}>
        <h1>{me?.signedIn ? "You are signed in" : "Sign in to IIP Academy"}</h1>
        <p>{me?.signedIn ? "Go to your dashboard to continue learning." : "Sign in with your work or school account to continue learning."}</p>
        {me?.signedIn ? <Link className="btn primary block" href="/academy">Go to dashboard</Link> : <button type="button" className="btn primary block" onClick={signIn}>Sign in</button>}
      </div>
    </div>
  );
}

export function AcademyNotFound() {
  return <PageState kind="notfound" title="Page not found" text="That page does not exist." action={{ label: "Back to dashboard", href: "/academy" }} />;
}
