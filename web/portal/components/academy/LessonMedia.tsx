"use client";

import { useEffect, useRef, useState } from "react";
import { academyMedia } from "@/lib/academy/api";

function LessonDownload({ base, file, label, lessonId }: { base: string; file: string; label: string; lessonId: string }) {
  const [url, setUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState("");
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    return () => { active.current = false; };
  }, []);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);
  const prepare = async () => {
    setBusy(true);
    setProblem("");
    try {
      const blob = await academyMedia(`${base}/${file}`);
      if (active.current) setUrl(URL.createObjectURL(blob));
    } catch {
      if (active.current) setProblem("The file could not be prepared. Retry the download.");
    } finally {
      if (active.current) setBusy(false);
    }
  };
  const name = label.replace("Download ", "");
  return <div>
    {url ? <a className="btn soft sm" href={url} download={`${lessonId}-${file}`}>Save {name}</a>
      : <button type="button" className="btn soft sm" disabled={busy} onClick={() => void prepare()}>{busy ? "Preparing " + name + "..." : label}</button>}
    {problem ? <p role="alert">{problem}</p> : null}
  </div>;
}

export function LessonMedia({ courseId, lessonId }: { courseId: string; lessonId: string }) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  const [problem, setProblem] = useState("");
  const [attempt, setAttempt] = useState(0);
  const base = `/courses/${encodeURIComponent(courseId)}/lessons/${encodeURIComponent(lessonId)}/media`;
  useEffect(() => {
    const controller = new AbortController();
    const created: string[] = [];
    let active = true;
    setUrls({});
    setProblem("");
    Promise.all(["video.mp4", "captions.vtt"].map(async (file) => {
      const blob = await academyMedia(`${base}/${file}`, controller.signal);
      if (!active) return [file, ""];
      const url = URL.createObjectURL(blob);
      created.push(url);
      return [file, url];
    })).then((entries) => { if (active) setUrls(Object.fromEntries(entries)); })
      .catch((failure) => { if (active) setProblem(failure instanceof Error ? failure.message : "Lesson media could not be loaded."); });
    return () => {
      active = false;
      controller.abort();
      created.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [base, attempt]);

  return <section aria-label="Lesson recording and downloads" style={{ marginBottom: 20 }}>
    {problem ? <div role="alert" className="card" style={{ padding: 20 }}>
      <p>{problem}</p><button className="btn soft" onClick={() => setAttempt((value) => value + 1)}>Retry media</button>
    </div> : urls["video.mp4"] ? <video key={urls["video.mp4"]} controls preload="metadata" playsInline aria-label="Lesson video"
      style={{ width: "100%", aspectRatio: "16 / 9", background: "var(--text)" }} src={urls["video.mp4"]}>
      <track src={urls["captions.vtt"]} kind="captions" srcLang="en" label="English" default />
    </video> : <p role="status">Loading lesson video and captions...</p>}
    <p style={{ color: "var(--muted)", fontSize: 13 }}>English captions use estimated timing and are awaiting editorial review. Use the transcript for the full narration.</p>
    <p style={{ color: "var(--muted)", fontSize: 13 }}>Choose a download, then select Save when the file is ready.</p>
    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
      {[["audio.mp3", "Download audio"], ["slides.pptx", "Download slides"], ["transcript.md", "Download transcript"]].map(([file, label]) =>
        <LessonDownload key={base + file} base={base} file={file} label={label} lessonId={lessonId} />)}
    </div>
  </section>;
}
