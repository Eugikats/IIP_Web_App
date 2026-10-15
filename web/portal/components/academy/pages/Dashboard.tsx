"use client";

import Link from "next/link";
import { useRef } from "react";
import { Icon } from "@/components/academy/Icon";
import { Ring } from "@/components/academy/Thumb";
import { CourseCard } from "@/components/academy/CourseCard";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { ZENITH_READY } from "@/lib/academy/flags";
import type { CourseSummary } from "@/lib/academy/types";
import { PageState, SkeletonCards } from "@/components/academy/PageState";

// The Academy dashboard, as designed: a welcome with the hero, three figures, the person's courses in a row,
// and the community and Zenith boxes. A visitor sees the same page with the catalogue where the person's courses
// would be, and honest figures about the Academy where theirs would be.

function WavingHand() {
  return (
    <svg className="wave" width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#F5A524" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ verticalAlign: "-6px" }}>
      <path d="M18 11V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2" />
      <path d="M14 10V4a2 2 0 0 0-2-2a2 2 0 0 0-2 2v2" />
      <path d="M10 10.5V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2v8" />
      <path d="M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15" />
    </svg>
  );
}

function Stat({ href, icon, value, full, short }: { href: string; icon: React.ReactNode; value: React.ReactNode; full: string; short: string }) {
  return (
    <Link className="stat card" href={href}>
      {icon}
      <div>
        <b>{value}</b>
        <span className="full">{full}</span>
        <span className="short">{short}</span>
      </div>
    </Link>
  );
}

export default function Dashboard() {
  const { me, learner, courses, coursesError, loadingMe, loadingLearner, percentOf, isEnrolled, hasCertificate, setZenithOpen } = useAcademy();
  const row = useRef<HTMLDivElement>(null);

  if (coursesError) return <PageState kind="error" title="The courses could not be loaded" text={coursesError} />;
  if (!courses || loadingMe) return <SkeletonCards />;

  const ready = courses.filter((course) => course.status === "ready");
  const mine = ready.filter((course) => isEnrolled(course.id));
  const active = mine.filter((course) => !hasCertificate(course.id));
  const myCourses: CourseSummary[] = [...active, ...mine.filter((course) => hasCertificate(course.id))];
  const signedIn = me?.signedIn === true;
  const firstName = (learner?.profile.name || (signedIn ? me.name : "")).split(" ")[0];

  const totalLessons = mine.reduce((sum, course) => sum + course.lessons, 0);
  const doneLessons = mine.reduce((sum, course) => sum + (learner?.progress[course.id]?.doneLessons.length ?? 0), 0);
  const overall = totalLessons ? Math.round((doneLessons / totalLessons) * 100) : 0;
  const certificates = learner?.certificates.length ?? 0;

  const shown = signedIn && myCourses.length ? myCourses : ready;

  const scroll = () => {
    const element = row.current;
    if (!element) return;
    const end = element.scrollLeft + element.clientWidth >= element.scrollWidth - 5;
    element.scrollLeft = end ? 0 : element.scrollLeft + element.clientWidth * 0.68;
  };

  return (
    <>
      <section className="hero card">
        <div className="hero-txt">
          <h1>{signedIn ? <>Welcome back{firstName ? `, ${firstName}` : ""} <WavingHand /></> : <>Welcome to IIP Academy <WavingHand /></>}</h1>
          <p>{signedIn ? "Continue learning. Build your skills for greater impact in insurance." : "Practical skills for insurance professionals. Learn at your own pace and earn a certificate."}</p>
        </div>
        <div className="hero-fig"><span className="arc" /><span className="beam" /><img src="/academy/hero.webp" alt="" /></div>
        <div className="quote">&ldquo;Practical skills.<br />Real impact.&rdquo;<i /></div>
      </section>

      <section className="stats">
        {signedIn ? (
          <>
            <Stat href="/academy/courses?show=mine" icon={<div className="ico blue"><Icon name="book" size={32} /></div>} value={active.length} full={active.length === 1 ? "Enrolled course" : "Enrolled courses"} short="Enrolled" />
            <Stat href="/academy/progress" icon={<div className="ico amber"><Ring percent={overall} size={40} /></div>} value={`${overall}%`} full="Overall progress" short="Progress" />
            <Stat href="/academy/certificates" icon={<div className="ico amber"><Icon name="trophy" size={32} /></div>} value={certificates} full={certificates === 1 ? "Certificate earned" : "Certificates earned"} short={certificates === 1 ? "Certificate" : "Certificates"} />
          </>
        ) : (
          <>
            <Stat href="/academy/courses" icon={<div className="ico blue"><Icon name="book" size={32} /></div>} value={ready.length} full={ready.length === 1 ? "Course to take now" : "Courses to take now"} short="Courses" />
            <Stat href="/academy/courses" icon={<div className="ico amber"><Icon name="video" size={32} /></div>} value={ready.reduce((sum, course) => sum + course.lessons, 0)} full="Lessons in all" short="Lessons" />
            <Stat href="/academy/courses" icon={<div className="ico amber"><Icon name="trophy" size={32} /></div>} value="Certificate" full="Of completion on every course" short="Certificate" />
          </>
        )}
      </section>

      <div className="sechead">
        <h2>{signedIn && myCourses.length ? "My courses" : "Courses"}</h2>
        <Link className="link" href="/academy/courses">View all courses <Icon name="arrowR" size={16} /></Link>
      </div>
      {signedIn && loadingLearner ? (
        <SkeletonCards count={3} />
      ) : signedIn && !myCourses.length ? (
        <>
          <section className="card newbie" data-testid="no-courses">
            <div className="nic"><Icon name="book" size={30} /></div>
            <h2>You have not started a course yet</h2>
            <p>Pick a course to begin. Your progress, notes and certificates will appear here.</p>
            <Link className="btn primary" href="/academy/courses">Browse courses <Icon name="arrowR" size={16} /></Link>
          </section>
          <div className="sechead"><h2>Courses you can take</h2></div>
          <div className="crow-wrap"><div className="crow" ref={row}>{ready.map((course) => <CourseCard key={course.id} course={course} />)}</div></div>
        </>
      ) : (
        <div className="crow-wrap">
          <div className="crow" id="crow" ref={row}>{shown.map((course) => <CourseCard key={course.id} course={course} />)}</div>
          <button className="carr" type="button" onClick={scroll} aria-label="More courses"><Icon name="chevR" size={22} /></button>
        </div>
      )}

      <section className="row2" style={ZENITH_READY ? undefined : { gridTemplateColumns: "1fr" }}>
        <div className="comm card">
          <div className="ci"><Icon name="users" size={28} /></div>
          <div className="ctx">
            <h3>Join the IIP Academy community</h3>
            <p>Connect with insurance professionals, share experiences, ask questions and learn from peers across different institutions.</p>
          </div>
          <div className="commbtns">
            <Link className="btn ghost" href="/academy/community">Go to community <Icon name="arrowR" size={16} /></Link>
          </div>
        </div>
        {ZENITH_READY ? (
          <div className="aimini card" id="aimini">
            <div className="aih"><Icon name="sparkle" size={22} />Zenith</div>
            <form className="minif" onSubmit={(event) => { event.preventDefault(); setZenithOpen(true); }}>
              <input placeholder="Ask Zenith anything..." autoComplete="off" />
              <button className="send" type="submit" aria-label="Ask Zenith"><Icon name="arrowUR" size={18} /></button>
            </form>
          </div>
        ) : null}
      </section>
    </>
  );
}
