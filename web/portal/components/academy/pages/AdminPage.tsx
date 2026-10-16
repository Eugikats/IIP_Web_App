"use client";

import { useCallback, useEffect, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { academyGet, academyPost, academyPut, academyDelete, AcademyApiError } from "@/lib/academy/api";
import { timeAgo, todayLabel } from "@/lib/academy/format";

type Submission = {
  userId: string; name: string; email: string | null; courseId: string; courseTitle: string; submittedAt: string;
  canvas: string[]; canvasBoxes: string[]; proposal: string; rubric: { criterion: string; marks: number; distinction: string }[]; passMark: number; outOf: number;
};
type LearnerRow = { userId: string; name: string; email: string | null };
type Reported = { id: string; author: string; role: string; text: string; hidden: boolean; reports: { reason: string; at: string }[] };
type CommunityBits = { discordUrl: string | null; groups: { id: string; name: string }[]; events: { id: string; title: string; startsAt: string; going: number }[] };

// The Academy team's screen: mark final tasks, enrol people, run the community's events and groups, and look at reports.
// Everything on it is refused by the server unless the person is an Academy operator, so this page only draws what the
// server has already agreed to give.
export default function AdminPage() {
  const { me, loadingMe } = useAcademy();
  const [tab, setTab] = useState<"marking" | "people" | "community" | "prices">("marking");

  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to manage the Academy" />;
  if (!me.isOperator) return <PageState kind="forbidden" title="This page is for the Academy team" text="You do not have access to manage the Academy." action={{ label: "Back to dashboard", href: "/academy" }} />;

  return (
    <>
      <div className="pagehead"><h1>Manage the Academy</h1><p>Mark final tasks, enrol people and run the community.</p></div>
      <div className="tabs chips" style={{ marginBottom: 18 }}>
        {([["marking", "Final tasks"], ["people", "People"], ["community", "Community"], ["prices", "Prices"]] as const).map(([id, label]) => (
          <button key={id} type="button" className={`chip${tab === id ? " on" : ""}`} onClick={() => setTab(id)} data-testid={`admin-tab-${id}`}>{label}</button>
        ))}
      </div>
      {tab === "marking" ? <Marking /> : tab === "people" ? <People /> : tab === "community" ? <CommunityAdmin /> : <Prices />}
    </>
  );
}

function useLoad<T>(path: string) {
  const [data, setData] = useState<T | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const load = useCallback(async () => {
    try {
      setData(await academyGet<T>(path));
      setProblem(null);
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? failure.message : "That could not be loaded.");
    }
  }, [path]);
  useEffect(() => { void load(); }, [load]);
  return { data, problem, load };
}

function Marking() {
  const { toast } = useAcademy();
  const { data, problem, load } = useLoad<{ submissions: Submission[] }>("/operator/finals");
  const [active, setActive] = useState<Submission | null>(null);
  const [marks, setMarks] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState("");
  const [busy, setBusy] = useState(false);

  if (problem) return <PageState kind="error" title="The final tasks could not be loaded" text={problem} />;
  if (!data) return <SkeletonCards count={1} />;

  const open = (item: Submission) => { setActive(item); setMarks({}); setFeedback(""); };
  const total = active ? active.rubric.reduce((sum, row) => sum + (Number(marks[row.criterion]) || 0), 0) : 0;

  const save = async () => {
    if (!active) return;
    for (const row of active.rubric) {
      const value = Number(marks[row.criterion]);
      if (marks[row.criterion] === undefined || marks[row.criterion] === "" || !Number.isInteger(value) || value < 0 || value > row.marks) {
        toast(`Give "${row.criterion}" a whole mark from 0 to ${row.marks}`, "x");
        return;
      }
    }
    setBusy(true);
    try {
      const result = await academyPost<{ score: number; passed: boolean }>("/operator/finals/mark", {
        userId: active.userId, courseId: active.courseId, feedback,
        marks: Object.fromEntries(active.rubric.map((row) => [row.criterion, Number(marks[row.criterion])])),
      });
      toast(result.passed ? `Marked: ${result.score} of ${active.outOf}, passed` : `Marked: ${result.score} of ${active.outOf}, not passed`);
      setActive(null);
      await load();
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "The marks could not be saved.", "x");
    } finally {
      setBusy(false);
    }
  };

  if (active) {
    return (
      <>
        <button type="button" className="btn ghost sm" onClick={() => setActive(null)} style={{ marginBottom: 14 }}><Icon name="chevL" size={15} />All submissions</button>
        <section className="card panel">
          <div className="lmeta">{active.courseTitle} · submitted {todayLabel(active.submittedAt)}</div>
          <h2 style={{ margin: "4px 0 12px" }}>{active.name}</h2>
          {active.canvasBoxes.map((label, index) => (
            <div key={index} style={{ marginTop: 12 }}><b>{index + 1}. {label}</b><p style={{ whiteSpace: "pre-wrap", lineHeight: 1.65, color: "#37415f" }}>{active.canvas[index]}</p></div>
          ))}
          <h3 style={{ marginTop: 18 }}>Proposal</h3>
          <p style={{ whiteSpace: "pre-wrap", lineHeight: 1.7, color: "#37415f" }}>{active.proposal}</p>
        </section>
        <section className="card panel" style={{ marginTop: 18 }}>
          <h2>Marks</h2>
          {active.rubric.map((row) => (
            <div key={row.criterion} className="setrow">
              <div><b>{row.criterion}</b><small>Out of {row.marks}. {row.distinction}</small></div>
              <input type="text" inputMode="numeric" value={marks[row.criterion] ?? ""} onChange={(event) => setMarks((current) => ({ ...current, [row.criterion]: event.target.value }))} aria-label={`Marks for ${row.criterion}`} style={{ width: 90, minWidth: 0 }} />
            </div>
          ))}
          <label className="field" style={{ marginTop: 14 }}>Feedback for the learner<textarea value={feedback} onChange={(event) => setFeedback(event.target.value)} maxLength={4000} /></label>
          <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
            <b>Total: {total} of {active.outOf}</b>
            <span style={{ color: "var(--muted)" }}>Pass mark {active.passMark}</span>
            <button type="button" className="btn primary" style={{ marginLeft: "auto" }} disabled={busy} onClick={() => void save()} data-testid="save-marks">{busy ? "Saving..." : "Save marks"}</button>
          </div>
        </section>
      </>
    );
  }

  return data.submissions.length ? (
    <section className="card panel" data-testid="submissions">
      <h2>Waiting for marking</h2>
      {data.submissions.map((item) => (
        <div key={`${item.userId}-${item.courseId}`} className="prow">
          <div className="grow"><b>{item.name}</b><div className="pc" style={{ fontSize: 13 }}>{item.courseTitle} · submitted {timeAgo(item.submittedAt)}</div></div>
          <button type="button" className="btn soft sm" onClick={() => open(item)}>Open</button>
        </div>
      ))}
    </section>
  ) : (
    <PageState kind="empty" title="Nothing to mark" text="Submitted final tasks will appear here." />
  );
}

function People() {
  const { toast, courses } = useAcademy();
  const { data, problem } = useLoad<{ learners: LearnerRow[] }>("/operator/learners");
  const [filter, setFilter] = useState("");
  const [course, setCourse] = useState("");
  const ready = (courses ?? []).filter((item) => item.status === "ready");

  if (problem) return <PageState kind="error" title="People could not be loaded" text={problem} />;
  if (!data) return <SkeletonCards count={1} />;

  const shown = data.learners.filter((row) => `${row.name} ${row.email ?? ""}`.toLowerCase().includes(filter.toLowerCase()));
  const enrol = async (row: LearnerRow) => {
    const target = course || ready[0]?.id;
    if (!target) return;
    try {
      const result = await academyPost<{ alreadyEnrolled: boolean }>("/operator/enrol", { userId: row.userId, courseId: target });
      toast(result.alreadyEnrolled ? `${row.name} was already enrolled` : `${row.name} is enrolled`);
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "That did not work.", "x");
    }
  };

  return (
    <section className="card panel">
      <h2>People</h2>
      <p style={{ color: "var(--muted)", marginBottom: 12 }}>People appear here once they have signed in to the Academy. Choose a course, then enrol them.</p>
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginBottom: 12 }}>
        <input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder="Search by name or email" aria-label="Search people" style={{ border: "1px solid var(--line)", borderRadius: 10, padding: "9px 12px", flex: 1, minWidth: 200 }} />
        <select value={course || ready[0]?.id || ""} onChange={(event) => setCourse(event.target.value)} aria-label="Course" style={{ border: "1px solid var(--line)", borderRadius: 10, padding: "9px 12px" }}>
          {ready.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
        </select>
      </div>
      {data.learners.length === 0 ? <div className="empty">Nobody has signed in yet.</div> : shown.length === 0 ? <div className="empty">No one matches that search.</div> : shown.map((row) => (
        <div key={row.userId} className="prow">
          <div className="grow"><b>{row.name}</b><div className="pc" style={{ fontSize: 13 }}>{row.email ?? "No email on the sign-in"}</div></div>
          <button type="button" className="btn soft sm" onClick={() => void enrol(row)}>Enrol</button>
        </div>
      ))}
    </section>
  );
}

function CommunityAdmin() {
  const { toast } = useAcademy();
  const bits = useLoad<CommunityBits>("/community");
  const reports = useLoad<{ reported: Reported[] }>("/operator/community/reports");
  const [discord, setDiscord] = useState<string | null>(null);
  const [event, setEvent] = useState({ title: "", startsAt: "", description: "", link: "" });
  const [group, setGroup] = useState({ name: "", description: "" });

  if (bits.problem) return <PageState kind="error" title="The community could not be loaded" text={bits.problem} />;
  if (!bits.data || !reports.data) return <SkeletonCards count={1} />;

  const run = async (work: () => Promise<unknown>, done: string) => {
    try {
      await work();
      toast(done);
      await Promise.all([bits.load(), reports.load()]);
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "That did not work.", "x");
    }
  };

  const discordValue = discord ?? bits.data.discordUrl ?? "";
  const field = { border: "1px solid var(--line)", borderRadius: 10, padding: "9px 12px", minWidth: 0 } as const;

  return (
    <>
      <section className="card panel">
        <h2>Discord</h2>
        <p style={{ color: "var(--muted)", marginBottom: 10 }}>The Discord buttons stay hidden until an invite link is saved here.</p>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <input value={discordValue} onChange={(event) => setDiscord(event.target.value)} placeholder="https://discord.gg/..." aria-label="Discord invite link" style={{ ...field, flex: 1, minWidth: 220 }} />
          <button type="button" className="btn primary sm" onClick={() => void run(() => academyPut("/operator/community/discord", { url: discordValue }), discordValue ? "Discord link saved" : "Discord link removed")} data-testid="save-discord">Save</button>
        </div>
      </section>

      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>Events</h2>
        {bits.data.events.length ? bits.data.events.map((item) => (
          <div key={item.id} className="prow">
            <div className="grow"><b>{item.title}</b><div className="pc" style={{ fontSize: 13 }}>{new Date(item.startsAt).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone: "Africa/Kampala" })} EAT · {item.going} going</div></div>
            <button type="button" className="btn ghost sm" onClick={() => void run(() => academyDelete(`/operator/community/events/${item.id}`), "Event removed")}>Remove</button>
          </div>
        )) : <p style={{ color: "var(--muted)" }}>No events are scheduled.</p>}
        <div style={{ display: "grid", gap: 10, marginTop: 14 }}>
          <input value={event.title} onChange={(e) => setEvent({ ...event, title: e.target.value })} placeholder="Event title" aria-label="Event title" style={field} />
          <input type="datetime-local" value={event.startsAt} onChange={(e) => setEvent({ ...event, startsAt: e.target.value })} aria-label="Starts at (East Africa Time)" style={field} />
          <input value={event.description} onChange={(e) => setEvent({ ...event, description: e.target.value })} placeholder="A line about it (optional)" aria-label="Event description" style={field} />
          <input value={event.link} onChange={(e) => setEvent({ ...event, link: e.target.value })} placeholder="https:// meeting link (optional)" aria-label="Event link" style={field} />
          <button
            type="button"
            className="btn primary sm"
            style={{ justifySelf: "start" }}
            data-testid="add-event"
            onClick={() => void run(async () => {
              await academyPost("/operator/community/events", { title: event.title, startsAt: event.startsAt ? `${event.startsAt}:00+03:00` : null, description: event.description, link: event.link });
              setEvent({ title: "", startsAt: "", description: "", link: "" });
            }, "Event added")}
          >
            Add event
          </button>
        </div>
      </section>

      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>Groups</h2>
        <p style={{ color: "var(--muted)", marginBottom: 10 }}>{bits.data.groups.map((item) => item.name).join(", ")}</p>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <input value={group.name} onChange={(e) => setGroup({ ...group, name: e.target.value })} placeholder="New group name" aria-label="Group name" style={{ ...field, flex: 1, minWidth: 180 }} />
          <input value={group.description} onChange={(e) => setGroup({ ...group, description: e.target.value })} placeholder="What it is for" aria-label="Group description" style={{ ...field, flex: 2, minWidth: 220 }} />
          <button type="button" className="btn primary sm" onClick={() => void run(async () => { await academyPost("/operator/community/groups", group); setGroup({ name: "", description: "" }); }, "Group added")}>Add group</button>
        </div>
      </section>

      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>Reported posts</h2>
        {reports.data.reported.length ? reports.data.reported.map((item) => (
          <div key={item.id} className="prow" style={{ alignItems: "flex-start" }}>
            <div className="grow">
              <b>{item.author}</b> <span className="pc" style={{ fontSize: 13 }}>{item.role}</span>
              <p style={{ margin: "6px 0", color: "#37415f" }}>{item.text}</p>
              <div className="pc" style={{ fontSize: 13 }}>{item.reports.length} {item.reports.length === 1 ? "report" : "reports"}{item.hidden ? ", hidden" : ""}</div>
            </div>
            <button type="button" className="btn soft sm" onClick={() => void run(() => academyPost(`/operator/community/posts/${item.id}/hide`, { hidden: !item.hidden }), item.hidden ? "Post restored" : "Post hidden")}>{item.hidden ? "Restore" : "Hide"}</button>
          </div>
        )) : <p style={{ color: "var(--muted)" }}>No reported posts.</p>}
      </section>
    </>
  );
}

function Prices() {
  const { toast, courses } = useAcademy();
  const { data, problem, load } = useLoad<{ currency: string; prices: { courseId: string; amount: number }[] }>("/pricing");
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ready = (courses ?? []).filter((item) => item.status === "ready");

  useEffect(() => {
    if (data) setDraft(Object.fromEntries(data.prices.map((price) => [price.courseId, String(price.amount)])));
  }, [data]);

  if (problem) return <PageState kind="error" title="Prices could not be loaded" text={problem} />;
  if (!data) return <SkeletonCards count={1} />;

  const invalid = ready.some((item) => {
    const value = draft[item.id];
    return value === undefined || value.trim() === "" || Number.isNaN(Number(value)) || Number(value) < 0 || Number(value) > 100_000_000;
  });
  const save = async () => {
    setBusy(true);
    try {
      await academyPut("/pricing", { prices: ready.map((item) => ({ courseId: item.id, amount: Math.round(Number(draft[item.id]) * 100) / 100 })) });
      toast("Prices saved");
      await load();
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "The prices could not be saved.", "x");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card panel">
      <h2>Course prices</h2>
      <p style={{ color: "var(--muted)", marginBottom: 12 }}>Learners see these on the course page. Payment is not connected yet, so nobody is charged. Use 0 for a free course.</p>
      {ready.map((item) => (
        <div key={item.id} className="setrow">
          <div><b>{item.title}</b></div>
          <span style={{ color: "var(--muted)" }}>{data.currency}</span>
          <input type="text" inputMode="decimal" value={draft[item.id] ?? ""} onChange={(event) => setDraft({ ...draft, [item.id]: event.target.value })} aria-label={`Price of ${item.title}`} style={{ width: 120, minWidth: 0 }} />
        </div>
      ))}
      <button type="button" className="btn primary" style={{ marginTop: 14 }} disabled={busy || invalid} onClick={() => void save()}>{busy ? "Saving..." : "Save prices"}</button>
    </section>
  );
}
