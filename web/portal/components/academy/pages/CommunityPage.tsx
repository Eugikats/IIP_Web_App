"use client";

import { useCallback, useEffect, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { Avatar } from "@/components/academy/Avatar";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { academyGet, academyPost, academyDelete, AcademyApiError } from "@/lib/academy/api";
import { timeAgo } from "@/lib/academy/format";

type Group = { id: string; name: string; description: string; icon: string; color: string; members: number; joined: boolean };
type EventItem = { id: string; title: string; startsAt: string; description: string; link: string | null; going: number; rsvp: boolean };
type Comment = { id: string; author: string; text: string; at: string; mine: boolean };
type Post = { id: string; author: string; role: string; tag: string; text: string; createdAt: string; likes: number; liked: boolean; mine: boolean; comments: Comment[] };
type CommunityState = { consent: boolean; discordUrl: string | null; joinedAny: boolean; groups: Group[]; events: EventItem[]; posts: Post[]; tags: string[] };

const eventTime = (iso: string) =>
  new Date(iso).toLocaleString("en-GB", { weekday: "short", day: "numeric", month: "short", hour: "numeric", minute: "2-digit", timeZone: "Africa/Kampala" }) + " EAT";

// The shared community: people from different insurers posting, replying, joining groups and events. Names and job titles
// are seen across organisations, so a person accepts a short notice before they post. Everything here is read from, and
// written to, the Academy's own records.
export default function CommunityPage() {
  const { me, loadingMe, learner, showModal, closeModal, toast } = useAcademy();
  const [state, setState] = useState<CommunityState | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [tab, setTab] = useState<"latest" | "popular" | "questions">("latest");
  const [text, setText] = useState("");
  const [tag, setTag] = useState("Claims");
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [posting, setPosting] = useState(false);

  const load = useCallback(async () => {
    try {
      setState(await academyGet<CommunityState>("/community"));
      setProblem(null);
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? failure.message : "The community could not be loaded.");
    }
  }, []);

  useEffect(() => { if (me?.signedIn) void load(); }, [me, load]);

  const act = async (work: () => Promise<unknown>, done?: string) => {
    try {
      await work();
      await load();
      if (done) toast(done);
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "That did not work. Try again.", "x");
    }
  };

  if (loadingMe) return <SkeletonCards count={2} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to join the community" text="The community is for people who have signed in to the Academy." />;
  if (problem) return <PageState kind="error" title="The community could not be loaded" text={problem} />;
  if (!state) return <SkeletonCards count={2} />;

  const firstName = (learner?.profile.name || me.name).split(" ")[0];
  const joined = state.groups.filter((group) => group.joined);

  const askConsent = (then: () => void) => {
    showModal(
      <>
        <h2>Before you post</h2>
        <p style={{ textAlign: "left", lineHeight: 1.7 }}>
          The community is shared by people from different insurers. When you post or reply, other members will see your name, job title and organisation.
        </p>
        <p style={{ textAlign: "left", lineHeight: 1.7, marginTop: 8 }}>
          Do not post customer names, phone numbers, policy or claim numbers, or anything your employer treats as confidential.
        </p>
        <div className="btns">
          <button type="button" className="btn ghost" onClick={closeModal}>Cancel</button>
          <button
            type="button"
            className="btn primary"
            data-testid="accept-consent"
            onClick={() => { closeModal(); void act(() => academyPost("/community/consent")).then(then); }}
          >
            I understand
          </button>
        </div>
      </>,
    );
  };

  const chooseGroups = () => {
    showModal(<GroupPicker groups={state.groups} onDone={async (join, leave) => {
      closeModal();
      await act(async () => {
        for (const id of join) await academyPost(`/community/groups/${id}/join`);
        for (const id of leave) await academyPost(`/community/groups/${id}/leave`);
      }, join.length || leave.length ? "Your groups are updated" : undefined);
    }} />, "wide");
  };

  const submitPost = () => {
    if (!text.trim()) { toast("Write something first", "pen"); return; }
    if (!state.consent) { askConsent(() => void sendPost()); return; }
    void sendPost();
  };

  // Sends the post. Consent has already been given by the time this runs, so it does not look at the stale state again.
  const sendPost = async () => {
    setPosting(true);
    try {
      await academyPost("/community/posts", { text, tag });
      setText("");
      setTab("latest");
      await load();
      toast("Posted to the community");
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "Your post could not be sent.", "x");
    } finally {
      setPosting(false);
    }
  };

  let posts = [...state.posts];
  if (tab === "popular") posts.sort((a, b) => b.likes - a.likes);
  if (tab === "questions") posts = posts.filter((post) => post.tag === "Question");

  return (
    <>
      <div className="pagehead row">
        <div><h1>Community</h1><p>Share experiences and learn from insurance professionals across institutions.</p></div>
        {state.discordUrl ? <a className="btn discord" href={state.discordUrl} target="_blank" rel="noopener noreferrer"><Icon name="discord" size={18} />Join us on Discord</a> : null}
      </div>
      <div className="cgrid">
        <div>
          {state.joinedAny ? (
            <section className="card composer">
              <Avatar name={learner?.profile.name || me.name} size={44} />
              <div style={{ flex: 1 }}>
                <textarea value={text} onChange={(event) => setText(event.target.value)} maxLength={2000} placeholder={`Share an experience or ask a question, ${firstName}...`} aria-label="Write a post" />
                <div className="cf">
                  <select value={tag} onChange={(event) => setTag(event.target.value)} aria-label="Topic">
                    {state.tags.map((item) => <option key={item}>{item}</option>)}
                  </select>
                  <button type="button" className="btn primary sm" style={{ marginLeft: "auto" }} disabled={posting} onClick={submitPost} data-testid="post"><Icon name="send" size={15} />Post</button>
                </div>
              </div>
            </section>
          ) : (
            <section className="card newbie">
              <div className="nic"><Icon name="userplus" size={30} /></div>
              <h2>Welcome to the community, {firstName}</h2>
              <p>Join a group to start posting, commenting and getting answers from peers in your field.</p>
              <button type="button" className="btn primary" onClick={chooseGroups} data-testid="join-group"><Icon name="userplus" size={17} />Join group</button>
            </section>
          )}

          <div className="tabs chips">
            {([["latest", "Latest"], ["popular", "Popular"], ["questions", "Questions"]] as const).map(([id, label]) => (
              <button key={id} type="button" className={`chip${tab === id ? " on" : ""}`} onClick={() => setTab(id)}>{label}</button>
            ))}
          </div>

          <div id="posts">
            {posts.length === 0 ? (
              <div className="empty" data-testid="no-posts">{state.posts.length === 0 ? "No posts yet. Be the first to share something." : "No questions yet."}</div>
            ) : posts.map((post) => (
              <article key={post.id} className="card post" data-testid="post-card">
                <div className="ph">
                  <Avatar name={post.author} size={44} />
                  <div><b>{post.author}</b><small>{post.role ? `${post.role} · ` : ""}{timeAgo(post.createdAt)}</small></div>
                  <span className={`tag ${post.tag}`}>{post.tag}</span>
                </div>
                <p className="tx">{post.text}</p>
                <div className="pa">
                  <button type="button" className={post.liked ? "liked" : ""} onClick={() => void act(() => academyPost(`/community/posts/${post.id}/like`))} aria-label={`Like, ${post.likes} likes`}><Icon name="heart" size={17} />{post.likes}</button>
                  <button type="button" onClick={() => setOpen((current) => { const next = new Set(current); if (next.has(post.id)) next.delete(post.id); else next.add(post.id); return next; })} data-testid="toggle-comments">
                    <Icon name="comment" size={17} />{post.comments.length} {post.comments.length === 1 ? "comment" : "comments"}
                  </button>
                  <button type="button" onClick={() => { void navigator.clipboard?.writeText(`${window.location.origin}/academy/community`).then(() => toast("Link copied to clipboard"), () => toast("The link could not be copied", "x")); }}><Icon name="share" size={17} />Share</button>
                  {post.mine ? (
                    <button type="button" onClick={() => void act(() => academyDelete(`/community/posts/${post.id}`), "Post removed")}><Icon name="x" size={17} />Remove</button>
                  ) : (
                    <button type="button" onClick={() => void act(() => academyPost(`/community/posts/${post.id}/report`, { reason: "Reported from the community page" }), "Thanks. The Academy team will take a look")}>Report</button>
                  )}
                </div>
                {open.has(post.id) ? (
                  <div className="cmts">
                    {post.comments.map((comment) => (
                      <div key={comment.id} className="cmt"><Avatar name={comment.author} size={32} /><div><b>{comment.author}</b>{comment.text}</div></div>
                    ))}
                    <CommentForm
                      onSend={async (value) => {
                        if (!state.consent) { askConsent(() => void act(() => academyPost(`/community/posts/${post.id}/comments`, { text: value }), "Reply posted")); return false; }
                        await act(() => academyPost(`/community/posts/${post.id}/comments`, { text: value }));
                        return true;
                      }}
                    />
                  </div>
                ) : null}
              </article>
            ))}
          </div>
        </div>

        <aside>
          {state.discordUrl ? (
            <section className="card side dc">
              <h3><Icon name="discord" size={20} />IIP Academy on Discord</h3>
              <p>Live chat, voice rooms and Q&amp;A sessions.</p>
              <a className="btn white sm" href={state.discordUrl} target="_blank" rel="noopener noreferrer">Open Discord <Icon name="ext" size={15} /></a>
            </section>
          ) : null}
          <section className="card side">
            <h3><Icon name="users" size={18} />{state.joinedAny ? "Your groups" : "Groups"}</h3>
            {state.joinedAny ? (
              <>
                {joined.map((group) => (
                  <div key={group.id} className="srow">
                    <div className="gi" style={{ background: group.color }}><Icon name={group.icon} size={18} /></div>
                    <div><b>{group.name}</b><small>{group.members.toLocaleString()} {group.members === 1 ? "member" : "members"}</small></div>
                  </div>
                ))}
                <button type="button" className="btn soft sm block" style={{ marginTop: 12 }} onClick={chooseGroups}>Manage groups</button>
              </>
            ) : (
              <>
                <p className="sidep">You have not joined any groups yet. Pick the ones that match your work.</p>
                <button type="button" className="btn primary sm block" onClick={chooseGroups}><Icon name="userplus" size={15} />Join group</button>
              </>
            )}
          </section>
          <section className="card side">
            <h3><Icon name="calendar" size={18} />Upcoming events</h3>
            {state.events.length ? state.events.map((item) => (
              <div key={item.id} className="srow">
                <div>
                  <b>{item.title}</b>
                  <small>{eventTime(item.startsAt)}{item.going ? ` · ${item.going} going` : ""}</small>
                  {item.link && item.rsvp ? <small><a href={item.link} target="_blank" rel="noopener noreferrer" style={{ color: "var(--blue)", fontWeight: 600 }}>Open the link</a></small> : null}
                </div>
                <button type="button" className={`btn ${item.rsvp ? "soft" : "ghost"} sm`} onClick={() => void act(async () => { const result = await academyPost<{ going: boolean; title: string }>(`/community/events/${item.id}/rsvp`); toast(result.going ? `You are going to ${result.title}` : "RSVP cancelled", "calendar"); })}>
                  {item.rsvp ? <><Icon name="check" size={14} />Going</> : "RSVP"}
                </button>
              </div>
            )) : <p className="sidep">No events are scheduled yet.</p>}
          </section>
        </aside>
      </div>
    </>
  );
}

function CommentForm({ onSend }: { onSend: (text: string) => Promise<boolean> }) {
  const [value, setValue] = useState("");
  return (
    <form
      className="cform"
      onSubmit={(event) => {
        event.preventDefault();
        if (!value.trim()) return;
        void onSend(value).then((sent) => { if (sent) setValue(""); });
      }}
    >
      <input value={value} onChange={(event) => setValue(event.target.value)} placeholder="Write a comment..." autoComplete="off" maxLength={800} aria-label="Write a comment" />
      <button className="send" type="submit" aria-label="Send comment"><Icon name="arrowUR" size={16} /></button>
    </form>
  );
}

function GroupPicker({ groups, onDone }: { groups: Group[]; onDone: (join: string[], leave: string[]) => void }) {
  const [chosen, setChosen] = useState<Set<string>>(new Set(groups.filter((group) => group.joined).map((group) => group.id)));
  const start = new Set(groups.filter((group) => group.joined).map((group) => group.id));
  const join = [...chosen].filter((id) => !start.has(id));
  const leave = [...start].filter((id) => !chosen.has(id));
  const { closeModal } = useAcademy();
  return (
    <>
      <h2>Choose your groups</h2>
      <p>Pick the groups that match your work. You can change these anytime.</p>
      <div className="gpick">
        {groups.map((group) => (
          <button
            key={group.id}
            type="button"
            className={`gopt${chosen.has(group.id) ? " on" : ""}`}
            aria-pressed={chosen.has(group.id)}
            onClick={() => setChosen((current) => { const next = new Set(current); if (next.has(group.id)) next.delete(group.id); else next.add(group.id); return next; })}
          >
            <span className="gi" style={{ background: group.color }}><Icon name={group.icon} size={18} /></span>
            <b>{group.name}</b>
            <small>{group.description}</small>
            <small className="gm">{group.members.toLocaleString()} {group.members === 1 ? "member" : "members"}</small>
            <span className="ck">{chosen.has(group.id) ? <Icon name="check" size={13} /> : null}</span>
          </button>
        ))}
      </div>
      <div className="btns">
        <button type="button" className="btn ghost" onClick={closeModal}>Cancel</button>
        <button type="button" className="btn primary" disabled={join.length + leave.length === 0} onClick={() => onDone(join, leave)} data-testid="save-groups">
          {join.length + leave.length === 0 ? "Select a group" : join.length && !leave.length ? `Join ${join.length} group${join.length > 1 ? "s" : ""}` : "Save groups"}
        </button>
      </div>
    </>
  );
}
