"use client";

import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { ZenithChat } from "@/components/academy/ZenithChat";
import { askZenith, resetZenith, useZenith } from "@/lib/academy/zenithStore";

const CAPS: [string, string, string, string][] = [
  ["target", "Recommend what is next", "Based on your progress", "What should I study next?"],
  ["bulb", "Explain a concept", "Grounded in your lessons", "What is the difference between generative AI and agentic AI?"],
  ["check", "Quiz you", "From units you have passed", "Quiz me"],
  ["calendar", "Plan your week", "Fits your daily goal", "Plan my study week"],
  ["award", "Track certificates", "What is left to earn", "How many certificates have I earned?"],
];

// The full Zenith page, as designed: the chat, what Zenith can do, and a log of what it did for this conversation.
export default function ZenithPage() {
  const { me, loadingMe, learner, courses } = useAcademy();
  const { log, busy } = useZenith();
  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to talk to Zenith" text="Zenith works from your own courses and progress, so it needs you to sign in." />;

  const mine = (courses ?? []).filter((course) => learner?.enrolled.some((entry) => entry.courseId === course.id));
  const lessonsDone = Object.values(learner?.progress ?? {}).reduce((sum, entry) => sum + entry.doneLessons.length, 0);

  return (
    <div className="aipage">
      <section className="card aichat">
        <div className="aihead">
          <div className="aav" style={{ width: 44, height: 44 }}><Icon name="sparkle" size={22} /></div>
          <div>
            <h2>Zenith</h2>
            <p className="aisub"><span className="live" />Connected to your {mine.length} {mine.length === 1 ? "course" : "courses"} and {lessonsDone} completed {lessonsDone === 1 ? "lesson" : "lessons"}</p>
          </div>
          <button type="button" className="btn ghost sm" onClick={resetZenith} disabled={busy}><Icon name="refresh" size={15} />New chat</button>
        </div>
        <ZenithChat autoFocus />
      </section>
      <aside>
        <section className="card side">
          <h3><Icon name="sparkle" size={18} />What I can do</h3>
          {CAPS.map(([icon, title, hint, question]) => (
            <button key={title} type="button" className="capr" onClick={() => void askZenith(question)} disabled={busy}>
              <span className="ci"><Icon name={icon} size={17} /></span>
              <span><b>{title}</b><small>{hint}</small></span>
            </button>
          ))}
        </section>
        <section className="card side">
          <h3><Icon name="target" size={18} />Agent activity</h3>
          <div className="actlog">
            {log.length === 0 ? <p className="sidep">What Zenith does will be listed here.</p> : [...log].reverse().map((line, index) => (
              <div key={index} className="alr"><time>{line.at}</time><Icon name="check" size={14} /><span>{line.text}</span></div>
            ))}
          </div>
        </section>
      </aside>
    </div>
  );
}
