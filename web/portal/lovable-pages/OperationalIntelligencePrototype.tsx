"use client";

import { useState } from "react";
import {
  Bell,
  ChevronRight,
  CircleHelp,
  FileCheck2,
  FolderSearch,
  ListTodo,
  Menu,
  MessageSquareText,
  PanelRightClose,
  Search,
  Send,
  ShieldCheck,
  Sparkles,
  UserRoundCheck,
  X,
} from "lucide-react";

type Surface = "work" | "evidence" | "approval" | "command";

const cases = [
  ["CLM-SYN-004287", "Riverbend Holdings", "Review conflicting evidence", "Evidence review", "1h 15m", "High", "78%"],
  ["CLM-SYN-004687", "Summit Contractors", "Prepare decision pack", "Human approval", "55m", "High", "81%"],
  ["CLM-SYN-004662", "Patel, Rohan", "Validate intake evidence", "Intake review", "6h 30m", "Medium", "65%"],
  ["CLM-SYN-004650", "Greenfield Retail Group", "Request missing document", "Evidence review", "1d", "Medium", "72%"],
] as const;

const nav = [
  ["work", "My work", ListTodo],
  ["evidence", "Claims", FolderSearch],
  ["approval", "Approvals", ShieldCheck],
  ["command", "Command Center", MessageSquareText],
] as const;

const titles: Record<Surface, [string, string, string]> = {
  work: ["YOUR ASSIGNED WORK", "My work", "Priority, ownership, evidence freshness, and the safe next action."],
  evidence: ["CASE · CLM-SYN-004287", "Evidence review", "Compare claim facts against current source evidence without leaving the work context."],
  approval: ["CASE · CLM-SYN-004687", "Prepare human decision", "AI prepares a proposal; an eligible person makes the accountable decision."],
  command: ["GROUNDED WORKSPACE", "Command Center", "Answers remain tied to permitted, current sources-not to conversation history alone."],
};

function StateTag({ children, tone = "blue" }: { children: React.ReactNode; tone?: "blue" | "violet" | "amber" | "green" }) {
  const tones = {
    blue: "border-blue-200 bg-blue-50 text-blue-800",
    violet: "border-violet-200 bg-violet-50 text-violet-800",
    amber: "border-amber-200 bg-amber-50 text-amber-900",
    green: "border-emerald-200 bg-emerald-50 text-emerald-800",
  };
  return <span className={`inline-flex items-center rounded-sm border px-2 py-1 text-xs font-semibold ${tones[tone]}`}>{children}</span>;
}

function EvidenceRail() {
  const sources = [
    ["PDF", "Inspection report.pdf", "Pages 4–6 · 16m ago", "Verified"],
    ["IMG", "Roof damage, north.jpg", "Photo · 21m ago", "Verified"],
    ["XLS", "Repair estimate.xlsx", "Financial · 9m ago", "Review"],
    ["PDF", "Motor policy.pdf", "Section 1.1 · 1h ago", "Verified"],
  ];
  return <aside aria-label="Current evidence" className="border-l border-slate-200 bg-[#f8fbff]">
    <div className="flex items-center justify-between border-b border-slate-200 px-4 py-4">
      <div><h2 className="text-sm font-bold text-slate-950">Evidence set</h2><p className="text-xs text-slate-600">Source, freshness, and review status</p></div>
      <button aria-label="Close evidence rail" className="rounded-sm p-2 text-slate-600 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"><PanelRightClose size={18} /></button>
    </div>
    <div className="space-y-1 p-3">
      {sources.map(([type, name, detail, status]) => <button key={name} className="flex w-full items-start gap-3 rounded-sm border border-transparent p-2 text-left hover:border-blue-200 hover:bg-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700">
        <span className="rounded-sm border border-slate-200 bg-white px-1.5 py-1 text-[10px] font-bold text-blue-800">{type}</span>
        <span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold text-slate-900">{name}</span><span className="block text-xs text-slate-600">{detail}</span></span>
        <span className={`text-[11px] font-semibold ${status === "Verified" ? "text-emerald-700" : "text-amber-800"}`}>{status}</span>
      </button>)}
      <button className="mt-3 flex min-h-11 w-full items-center justify-center rounded-sm border border-blue-200 bg-white px-3 text-sm font-semibold text-blue-800 hover:bg-blue-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700">View all evidence <ChevronRight size={16} /></button>
    </div>
  </aside>;
}

function Work({ open }: { open: (surface: Surface) => void }) {
  return <div className="space-y-5">
    <section aria-label="Actionable work summary" className="grid gap-3 sm:grid-cols-4">
      {[["7", "Urgent", "amber"], ["12", "Due today", "blue"], ["9", "AI drafts", "violet"], ["92%", "SLA met · 7d", "green"]].map(([value, label, tone]) => <button key={label} onClick={() => open("evidence")} className="min-h-24 rounded-sm border border-slate-200 bg-white p-4 text-left hover:border-blue-300 hover:bg-blue-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"><span className={`block text-2xl font-bold ${tone === "amber" ? "text-amber-800" : tone === "violet" ? "text-violet-800" : tone === "green" ? "text-emerald-800" : "text-blue-800"}`}>{value}</span><span className="text-sm text-slate-700">{label}</span></button>)}
    </section>
    <section className="overflow-hidden rounded-sm border border-slate-200 bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4"><div><h2 className="font-bold text-slate-950">Priority queue</h2><p className="text-sm text-slate-600">Assigned work sorted by SLA, evidence freshness, and priority.</p></div><button className="min-h-10 rounded-sm border border-blue-700 bg-blue-700 px-3 text-sm font-semibold text-white hover:bg-blue-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700">My claims</button></div>
      <div className="overflow-x-auto"><table className="w-full min-w-[760px] text-left text-sm"><caption className="sr-only">Synthetic priority work queue</caption><thead className="bg-[#f8fbff] text-xs uppercase tracking-wide text-slate-600"><tr><th className="px-5 py-3">Priority</th><th className="px-3 py-3">Case</th><th className="px-3 py-3">Task</th><th className="px-3 py-3">Work state</th><th className="px-3 py-3">SLA</th><th className="px-3 py-3">AI confidence</th><th className="px-5 py-3"><span className="sr-only">Open</span></th></tr></thead><tbody className="divide-y divide-slate-100">
        {cases.map(([id, claimant, task, stage, sla, priority, confidence]) => <tr key={id} className="hover:bg-blue-50"><td className="px-5 py-3"><StateTag tone={priority === "High" ? "amber" : "blue"}>{priority} priority</StateTag></td><td className="px-3 py-3"><span className="block font-semibold text-slate-950">{id}</span><span className="block text-xs text-slate-600">{claimant}</span></td><td className="px-3 py-3 text-slate-800">{task}</td><td className="px-3 py-3"><StateTag tone={stage === "Human approval" ? "amber" : "blue"}>{stage}</StateTag></td><td className="px-3 py-3 font-semibold text-slate-800">{sla}</td><td className="px-3 py-3"><span className="font-semibold text-slate-800">{confidence}</span><span className="ml-2 inline-block h-1.5 w-12 overflow-hidden rounded-full bg-slate-200 align-middle"><span className="block h-full bg-violet-600" style={{ width: confidence }} /></span></td><td className="px-5 py-3"><button aria-label={`Open ${id}`} onClick={() => open(stage === "Human approval" ? "approval" : "evidence")} className="rounded-sm p-2 text-blue-800 hover:bg-blue-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"><ChevronRight size={18} /></button></td></tr>)}
      </tbody></table></div>
    </section>
  </div>;
}

function Evidence({ open }: { open: (surface: Surface) => void }) {
  return <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_300px]"><div className="space-y-5"><section className="rounded-sm border border-slate-200 bg-white"><div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-200 px-5 py-4"><div><h2 className="font-bold text-slate-950">Storm damage · Motor comprehensive</h2><p className="text-xs text-slate-600">Reported 08 May 2026 · owner: A. Ndlovu · evidence set EV-SYN-03</p></div><StateTag tone="amber">High priority</StateTag></div><nav aria-label="Case workspace" className="flex gap-5 overflow-x-auto border-b border-slate-200 px-5 text-sm font-semibold"><button className="border-b-2 border-transparent py-3 text-slate-600">Summary</button><button className="border-b-2 border-blue-700 py-3 text-blue-800">Evidence</button><button className="border-b-2 border-transparent py-3 text-slate-600">AI findings</button><button className="border-b-2 border-transparent py-3 text-slate-600">Policy & cover</button></nav><div className="grid lg:grid-cols-[185px_minmax(0,1fr)]"><aside className="border-b border-r border-slate-200 bg-[#f8fbff] p-4 lg:border-b-0"><h3 className="text-xs font-bold uppercase tracking-wide text-slate-700">Evidence index</h3><dl className="mt-4 space-y-3 text-sm text-slate-700"><div className="flex justify-between"><dt>All documents</dt><dd className="font-bold">8</dd></div><div className="flex justify-between"><dt>Insured statements</dt><dd className="font-bold">2</dd></div><div className="flex justify-between"><dt>Photos & video</dt><dd className="font-bold">2</dd></div></dl></aside><article className="p-5"><h3 className="font-bold text-slate-950">Inspection report.pdf</h3><p className="text-xs font-semibold text-emerald-700">Verified source · pages 4–6</p><div className="mt-4 min-h-72 border border-slate-200 bg-slate-50 p-6 text-sm leading-7 text-slate-800"><p className="font-semibold">Loss description</p><p className="mt-3">The source report records wind damage to the north roofline. The policy is in force at the reported time. The repair estimate appears inconsistent with the inspection scope and requires human review.</p><hr className="my-5 border-slate-200" /><p><strong>Cited source:</strong> Inspection report.pdf, pages 4–6.</p></div></article></div></section><section className="rounded-sm border border-violet-200 bg-violet-50/60 p-4"><div className="flex gap-3"><Sparkles className="mt-0.5 shrink-0 text-violet-700" size={18} /><div><div className="flex flex-wrap items-center gap-2"><h2 className="font-bold text-violet-950">AI-prepared finding</h2><StateTag tone="violet">Draft · requires human review</StateTag></div><p className="mt-2 text-sm text-violet-950">The repair estimate exceeds the source-supported scope. Verify the contractor quote before a recommendation is prepared.</p><button onClick={() => open("approval")} className="mt-3 min-h-10 rounded-sm border border-violet-300 bg-white px-3 text-sm font-semibold text-violet-950 hover:bg-violet-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-violet-700">Prepare decision pack</button></div></div></section></div><EvidenceRail /></div>;
}

function Approval({ confirm }: { confirm: () => void }) {
  return <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_300px]"><div className="space-y-5"><section className="rounded-sm border border-slate-200 bg-white"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4"><div><h2 className="font-bold text-slate-950">Decision preparation</h2><p className="text-sm text-slate-700">Review current sources, proposal scope, and authority before a human outcome.</p></div><StateTag tone="amber">Human decision required</StateTag></div><div className="grid gap-5 p-5 lg:grid-cols-2"><section className="border border-violet-200 bg-violet-50/60 p-4"><div className="flex items-center gap-2"><Sparkles size={18} className="text-violet-700" /><h2 className="font-bold text-violet-950">AI recommendation</h2></div><p className="mt-4 text-2xl font-bold text-slate-950">Approve with conditions</p><p className="mt-4 text-sm text-slate-800">Policy is in force. Source evidence supports roofline repair, but the contractor quote requires verification before any consequential outcome.</p><p className="mt-4 text-sm font-semibold text-slate-900">Recommended amount · UGX 94.3m–101.2m</p></section><section className="border-2 border-amber-300 bg-amber-50/40 p-4"><div className="flex items-center gap-2"><UserRoundCheck size={18} className="text-amber-800" /><h2 className="font-bold text-amber-950">Accountable human outcome</h2></div><p className="mt-2 text-sm text-amber-950">Only an eligible approver may record this decision.</p><div className="mt-4 space-y-2 text-sm"><label className="flex items-center gap-2 border border-amber-200 bg-white p-3"><input type="radio" name="decision" defaultChecked />Approve with conditions</label><label className="flex items-center gap-2 border border-amber-200 bg-white p-3"><input type="radio" name="decision" />Return for evidence</label><label className="flex items-center gap-2 border border-amber-200 bg-white p-3"><input type="radio" name="decision" />Decline proposal</label></div><button onClick={confirm} className="mt-4 flex min-h-11 w-full items-center justify-center gap-2 rounded-sm bg-blue-700 px-4 text-sm font-semibold text-white hover:bg-blue-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"><ShieldCheck size={17} />Record simulated human outcome</button></section></div></section><section className="flex gap-3 border border-slate-200 bg-white p-4"><FileCheck2 className="shrink-0 text-emerald-700" size={20} /><p className="text-sm text-slate-700"><strong className="block text-slate-950">Decision prerequisites</strong>Evidence set current · proposal version PR-SYN-07 · approver eligibility confirmed · no execution has started.</p></section></div><EvidenceRail /></div>;
}

function Command() {
  return <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_300px]"><section className="border border-slate-200 bg-white"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4"><div><h2 className="font-bold text-slate-950">Claim intelligence conversation</h2><p className="text-sm text-slate-700">Every material answer names its source and confidence boundary.</p></div><StateTag tone="violet">AI prepared</StateTag></div><div className="space-y-5 p-5"><p className="ml-auto max-w-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-800">What is the evidence basis for the AI assessment of building damage?</p><article className="max-w-3xl border border-slate-200 bg-[#f8fbff] p-4"><div className="flex items-center gap-2"><Sparkles size={16} className="text-violet-700" /><span className="text-xs font-bold uppercase tracking-wide text-violet-900">AI-prepared response</span></div><p className="mt-3 text-sm leading-6 text-slate-800">The assessment is supported by the inspection report’s documented roofline damage and policy wording on windstorm cover. The repair estimate is marked for human review because its scope does not fully match the inspection evidence.</p><h3 className="mt-4 font-bold text-slate-950">Cited sources</h3><ol className="mt-2 space-y-2 text-sm text-blue-800"><li>1. Inspection report.pdf · pages 4–6</li><li>2. Roof damage, north.jpg · photo</li><li>3. Motor policy.pdf · section 1.1</li></ol></article></div><form className="flex gap-2 border-t border-slate-200 p-4" onSubmit={(event) => event.preventDefault()}><label className="sr-only" htmlFor="question">Ask a question about this claim</label><input id="question" className="min-h-11 flex-1 rounded-sm border border-slate-300 px-3 text-sm focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-blue-700" placeholder="Ask a question about this claim…" /><button type="submit" aria-label="Send question" className="min-h-11 rounded-sm bg-blue-700 px-4 text-white hover:bg-blue-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700"><Send size={18} /></button></form></section><EvidenceRail /></div>;
}

export default function OperationalIntelligencePrototype() {
  const [surface, setSurface] = useState<Surface>("work");
  const [menuOpen, setMenuOpen] = useState(false);
  const [notice, setNotice] = useState("Prototype only: synthetic data and simulated actions. No API, provider, or decision write occurs.");
  const [eyebrow, title, detail] = titles[surface];
  const open = (next: Surface) => { setSurface(next); setMenuOpen(false); setNotice(`${titles[next][1]} opened in this development-only prototype.`); };

  return <main className="min-h-screen bg-[#f2f6fa] text-slate-900"><div className="border-b border-blue-200 bg-blue-50 px-4 py-2 text-center text-xs font-medium text-blue-950" role="status">Development-only prototype · synthetic data · no production workflow or decision behavior</div><div className="min-h-[calc(100vh-33px)] lg:grid lg:grid-cols-[244px_minmax(0,1fr)]">
    <aside className="hidden border-r border-slate-200 bg-white lg:block"><div className="flex h-16 items-center gap-3 border-b border-slate-200 px-5"><span className="grid h-8 w-8 place-items-center rounded-sm bg-blue-700 text-sm font-black text-white">II</span><span className="font-bold tracking-tight text-slate-950">IIP Operations</span></div><nav className="p-3" aria-label="Prototype sections">{nav.map(([id, label, Icon]) => <button key={id} onClick={() => open(id)} className={`mb-1 flex min-h-11 w-full items-center gap-3 rounded-sm border-l-2 px-3 text-left text-sm font-semibold focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 ${surface === id ? "border-blue-700 bg-blue-50 text-blue-900" : "border-transparent text-slate-700 hover:bg-slate-50"}`} aria-current={surface === id ? "page" : undefined}><Icon size={18} />{label}</button>)}</nav><div className="mx-4 mt-8 border-t border-slate-200 pt-4 text-xs text-slate-600"><p className="font-semibold text-slate-800">Nile region · Training</p><p className="mt-1">Workspace context is visible, not authority.</p></div></aside>
    <section className="min-w-0"><header className="flex min-h-16 items-center justify-between border-b border-slate-200 bg-white px-4 sm:px-6"><div className="flex items-center gap-3"><button aria-label="Open prototype navigation" onClick={() => setMenuOpen(true)} className="rounded-sm p-2 text-slate-700 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 lg:hidden"><Menu size={22} /></button><div><p className="text-[11px] font-bold tracking-[0.14em] text-blue-800">{eyebrow}</p><h1 className="font-bold text-slate-950">{title}</h1></div></div><div className="flex items-center gap-2"><button aria-label="Search" className="rounded-sm p-2 text-slate-600 hover:bg-slate-100"><Search size={19} /></button><button aria-label="Notifications" className="rounded-sm p-2 text-slate-600 hover:bg-slate-100"><Bell size={19} /></button><span className="grid h-8 w-8 place-items-center rounded-full bg-blue-950 text-xs font-bold text-white">AM</span></div></header>
      {menuOpen && <div className="fixed inset-0 z-20 lg:hidden"><button aria-label="Close navigation overlay" onClick={() => setMenuOpen(false)} className="absolute inset-0 bg-slate-950/30" /><aside className="relative h-full w-72 bg-white p-4 shadow-xl"><div className="mb-5 flex items-center justify-between"><span className="font-bold">IIP Operations</span><button aria-label="Close navigation" onClick={() => setMenuOpen(false)} className="rounded-sm p-2 hover:bg-slate-100"><X size={20} /></button></div>{nav.map(([id, label, Icon]) => <button key={id} onClick={() => open(id)} className="mb-1 flex min-h-12 w-full items-center gap-3 rounded-sm px-3 text-left text-sm font-semibold text-slate-800 hover:bg-blue-50"><Icon size={18} />{label}</button>)}</aside></div>}
      <div className="px-4 py-6 sm:px-6 lg:px-8"><div className="mb-5 flex flex-wrap items-end justify-between gap-3"><p className="max-w-2xl text-sm text-slate-700">{detail}</p><p className="text-xs text-slate-600">Synthetic workspace · current as 10:42</p></div>{surface === "work" && <Work open={open} />}{surface === "evidence" && <Evidence open={open} />}{surface === "approval" && <Approval confirm={() => setNotice("Simulated human outcome recorded. No decision, workflow, audit event, or external effect was created.")} />}{surface === "command" && <Command />}<p className="mt-5 flex items-center gap-2 border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-950" role="status"><CircleHelp size={17} className="shrink-0" />{notice}</p></div>
    </section>
  </div></main>;
}
