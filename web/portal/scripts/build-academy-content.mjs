// Builds the Academy's course content as one JSON file for the API to serve.
//
//   node scripts/build-academy-content.mjs
//
// Sources, none of which are edited:
//   * docs/iip-academy/academy-content/*.md  : Course 1 research packs, module assessments, final task, Course 2 map.
//   * lib/academy/*.ts                        : the other courses already written for IIP Academy.
// Output: src/InsuranceIntelligence.Api/Academy/Content/academy-content.json (embedded in the API).
//
// The supplied markdown uses em dashes. Nothing shown to a person may contain one, so they are converted here and the
// build fails if any survives.

import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const portal = path.resolve(here, "..");
const repo = path.resolve(portal, "..", "..");
const contentDir = path.join(repo, "docs", "iip-academy", "academy-content");
const outFile = path.join(repo, "src", "InsuranceIntelligence.Api", "Academy", "Content", "academy-content.json");
const require = createRequire(import.meta.url);
const ts = require(path.join(portal, "node_modules", "typescript"));

// ---------------------------------------------------------------- text helpers

const EM = "—";

/** Em dashes become the punctuation a person would write. No em dash may remain. */
function clean(text) {
  return text
    .replace(/\*\*\s*—\s*/g, "**: ")
    .replace(/\s+—\s+/g, ", ")
    .replace(/—/g, ", ")
    .replace(/\*verify section\*/gi, "section to be confirmed")
    .replace(/\*verify\*/gi, "to be confirmed")
    .replace(/\*to source\*/gi, "to be sourced")
    .replace(/\s+,/g, ",")
    .replace(/,\s*,/g, ",")
    .trim();
}

const words = (text) => (text.match(/\S+/g) ?? []).length;
const slug = (text) => text.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

// ---------------------------------------------------------------- the existing courses (TypeScript)

function loadTs(entry) {
  const cache = new Map();
  const resolve = (from, request) => {
    const base = request.startsWith("@/") ? path.join(portal, request.slice(2)) : path.resolve(path.dirname(from), request);
    for (const candidate of [base + ".ts", base + ".tsx", path.join(base, "index.ts")]) if (fs.existsSync(candidate)) return candidate;
    return null;
  };
  const load = (file) => {
    if (cache.has(file)) return cache.get(file).exports;
    const source = fs.readFileSync(file, "utf8");
    const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020, esModuleInterop: true } }).outputText;
    const module = { exports: {} };
    cache.set(file, module);
    const localRequire = (request) => {
      const target = resolve(file, request);
      if (target) return load(target);
      return require(request);
    };
    vm.runInNewContext(`(function (exports, require, module) {${js}\n})`, { console })(module.exports, localRequire, module);
    return module.exports;
  };
  return load(entry);
}

const CATEGORY_STYLE = {
  "ai-foundations": { hue: ["#0E2A8C", "#3D7BF0"], icon: "sparkle" },
  "claims-fraud": { hue: ["#13227A", "#6F8BF5"], icon: "shield" },
  "underwriting-risk": { hue: ["#0B6E6E", "#2BB3A3"], icon: "layers" },
  "sales-experience": { hue: ["#1E4FD8", "#5B9BF5"], icon: "chart" },
  "build-govern": { hue: ["#334155", "#7C8DB0"], icon: "briefcase" },
};

const PASS_CHECK = 60;

function lessonsFromModule(courseId, module, moduleIndex) {
  const idea = [];
  idea.push({ type: "p", text: module.goal });
  if (module.outcomes?.length) idea.push({ type: "h", text: "You will be able to" }, { type: "list", items: module.outcomes });
  if (module.flow?.length) idea.push({ type: "h", text: "The idea in one picture" }, { type: "flow", steps: module.flow, note: module.flowNote ?? null });
  if (module.screen) {
    idea.push({ type: "h", text: "Where it lives in IIP" });
    idea.push(
      "missing" in module.screen
        ? { type: "screenMissing", text: module.screen.missing }
        : { type: "screen", file: module.screen.file, caption: module.screen.caption, callouts: module.screen.callouts ?? [] },
    );
  }

  const example = [];
  if (module.example) example.push({ type: "example", title: module.example.title, steps: module.example.steps });
  if (module.takeaways?.length) example.push({ type: "takeaways", items: module.takeaways });

  const make = (suffix, title, blocks, tryIt) => {
    const minutes = Math.max(3, Math.round(blocks.reduce((sum, b) => sum + words(JSON.stringify(b)), 0) / 180));
    return { id: `${module.id}-${suffix}`, title, minutes, outcome: module.goal, blocks, sources: [], tryIt: tryIt ?? null, video: null, quiz: null };
  };

  return [
    make("idea", `${module.title}: the idea and where it lives in IIP`, idea, null),
    make("example", `${module.title}: a worked example`, example, module.tryIt ?? null),
  ];
}

function existingCourses() {
  const { COURSES } = loadTs(path.join(portal, "lib", "academy", "courses.ts"));
  return COURSES.map((course) => {
    const style = CATEGORY_STYLE[course.category] ?? CATEGORY_STYLE["build-govern"];
    return {
      id: course.id,
      category: course.category,
      title: course.title,
      short: course.short,
      description: course.aim,
      audience: course.audience,
      duration: course.duration,
      level: null,
      hue: style.hue,
      icon: style.icon,
      status: course.modules.length > 0 ? "ready" : "coming_soon",
      outcomes: course.outcomes ?? [],
      who: [course.audience],
      iipAreas: course.iipAreas ?? [],
      modules: course.modules.map((module, index) => ({
        id: module.id,
        title: module.title,
        lessons: lessonsFromModule(course.id, module, index),
        assessment: null,
      })),
      outline: (course.outline ?? []).map((item) => ({ module: null, unit: item.title, outcome: item.goal })),
      check: course.quiz?.length
        ? { id: `${course.id}-check`, title: "Check your understanding", passMark: PASS_CHECK, maxAttempts: 0, questions: course.quiz.map((q, i) => ({ id: `${course.id}-q${i + 1}`, q: q.q, options: q.options, answer: q.answer, why: q.why })) }
        : null,
      finalTask: null,
      requirements: "Complete every lesson and pass the check.",
      sources: [],
    };
  });
}

// ---------------------------------------------------------------- Course 1 (markdown)

function readContent(name) {
  return fs.readFileSync(path.join(contentDir, name), "utf8").replace(/\r\n/g, "\n");
}

/** The master register's source table: id, title, type, date, verification. */
function readSources() {
  const text = readContent("00_Master_Curriculum_and_Research_Register.md");
  const sources = {};
  for (const line of text.split("\n")) {
    const cells = line.split("|").map((c) => c.trim());
    if (cells.length < 10 || !/^S\d\d$/.test(cells[1])) continue;
    sources[cells[1]] = { id: cells[1], title: clean(cells[2]), type: clean(cells[3]), date: clean(cells[6] ?? ""), status: clean((cells[8] ?? "").replace(/\*\*/g, "")) };
  }
  return sources;
}

/** Splits a section's lines into paragraph, list and table blocks. */
function blocksFrom(lines) {
  const blocks = [];
  let paragraph = [];
  const flush = () => {
    if (paragraph.length) blocks.push({ type: "p", text: clean(paragraph.join(" ")) });
    paragraph = [];
  };
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (!line.trim()) {
      flush();
    } else if (line.trim().startsWith("|")) {
      flush();
      const rows = [];
      while (i < lines.length && lines[i].trim().startsWith("|")) rows.push(lines[i++]);
      i--;
      const cells = (row) => row.trim().replace(/^\||\|$/g, "").split("|").map((c) => clean(c.trim()) || "None");
      const head = cells(rows[0]);
      const body = rows.slice(2).map(cells);
      blocks.push({ type: "table", head, rows: body });
    } else if (/^\s*(-|\*)\s+/.test(line) && !line.trim().startsWith("**")) {
      flush();
      const items = [];
      while (i < lines.length && /^\s*(-|\*)\s+/.test(lines[i])) items.push(clean(lines[i++].replace(/^\s*(-|\*)\s+/, "")));
      i--;
      blocks.push({ type: "list", items });
    } else if (/^\s*\d+\.\s+/.test(line) && !/^\s*\d+\.\s+\*\*/.test(line)) {
      flush();
      const items = [];
      while (i < lines.length && /^\s*\d+\.\s+/.test(lines[i])) items.push(clean(lines[i++].replace(/^\s*\d+\.\s+/, "")));
      i--;
      blocks.push({ type: "list", items, ordered: true });
    } else if (/^\*Validate/i.test(line.trim())) {
      // A note to the content team, not to the learner.
    } else {
      paragraph.push(line.trim());
    }
  }
  flush();
  return blocks;
}

const SECTION_TITLES = {
  "Core concepts and terminology": "Core ideas",
  "Regulatory and operational considerations": "Regulation and practice",
  "Realistic Ugandan scenarios": "Scenarios from Ugandan insurance",
  "Common professional mistakes": "Common mistakes",
  "Recommended techniques": "Techniques to use",
  "AI applications and limitations": "Where it helps and where it fails",
};

/** Four options on one line: "A) text B) text C) text D) text". */
function splitOptions(text) {
  const marks = [...text.matchAll(/(?:^|\s)([A-D])\)\s/g)];
  const options = [];
  for (let i = 0; i < marks.length; i++) {
    const start = marks[i].index + marks[i][0].length;
    const end = i + 1 < marks.length ? marks[i + 1].index : text.length;
    options.push(clean(text.slice(start, end)));
  }
  return options;
}

/** The ten questions of a unit's "Assessment questions" section. */
function parseUnitQuestions(lines, idPrefix) {
  const questions = [];
  for (let i = 0; i < lines.length; i++) {
    const head = /^\s*(\d+)\.\s+\*\*(.+)\*\*\s*$/.exec(lines[i]);
    if (!head) continue;
    const optionLine = lines[i + 1] ?? "";
    const answerLine = lines[i + 2] ?? "";
    const answer = /\*\*Answer:\s*([A-D])\.\*\*\s*(.*)$/.exec(answerLine);
    const options = splitOptions(optionLine.trim());
    if (!answer || options.length < 2) throw new Error(`Could not read question ${head[1]} of ${idPrefix}`);
    questions.push({ id: `${idPrefix}-q${head[1]}`, q: clean(head[2]), options, answer: "ABCD".indexOf(answer[1]), why: clean(answer[2]) });
  }
  return questions;
}

/** Module assessments: "n. **Question** A) .. B) .. C) .. D) .. , **X.** why" on one line. */
function parseModuleAssessments() {
  const text = readContent("C01_Assessments_Module_and_Final.md");
  const result = {};
  const sections = text.split(/^## /m).slice(1);
  for (const section of sections) {
    const title = /^Module (\d) Assessment/.exec(section);
    if (!title) continue;
    const questions = [];
    for (const line of section.split("\n")) {
      const match = /^(\d+)\.\s+\*\*(.+?)\*\*\s+(A\).*?)\s+—\s+\*\*([A-D])\.\*\*\s*(.*)$/.exec(line);
      if (!match) continue;
      const options = splitOptions(match[3]);
      if (options.length < 2) throw new Error(`Could not read options of module ${title[1]} question ${match[1]}`);
      questions.push({ id: `m${title[1]}-a${match[1]}`, q: clean(match[2]), options, answer: "ABCD".indexOf(match[4]), why: clean(match[5]) || "See the unit this question comes from." });
    }
    if (questions.length !== 15) throw new Error(`Module ${title[1]} assessment has ${questions.length} questions, expected 15`);
    result[title[1]] = questions;
  }
  return result;
}

function parseFinalTask() {
  const text = readContent("C01_Assessments_Module_and_Final.md");
  const start = text.indexOf("## Final Applied Course Task");
  const section = text.slice(start);
  const title = clean(/^## Final Applied Course Task\s+—\s+(.+)$/m.exec(section)?.[1] ?? "AI Use-Case Proposal and Governance Review");
  const part = (name) => {
    const m = new RegExp(`### ${name}[^\\n]*\\n([\\s\\S]*?)(?=\\n### |$)`).exec(section);
    return m ? m[1].trim() : "";
  };
  const rubricLines = part("Marking rubric").split("\n").filter((l) => l.trim().startsWith("|")).slice(2);
  const rubric = rubricLines.map((line) => {
    const cells = line.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
    return { criterion: clean(cells[0]), marks: Number(cells[1]), distinction: clean(cells[2] ?? "") };
  });
  if (rubric.reduce((sum, r) => sum + r.marks, 0) !== 100) throw new Error("The rubric does not total 100");
  return {
    id: "ai-agentic-insurance-final",
    title,
    brief: clean(part("Brief")),
    deliverables: clean(part("Deliverables")),
    rubric,
    passMark: 60,
    outOf: 100,
    integrity: clean(part("Integrity measures")),
    requirement: clean(part("Competency verification and certificate")),
    canvasBoxes: [
      "Problem and baseline", "Proposed AI solution", "Users and affected people", "Data", "Autonomy and decision rights",
      "Value", "Risks", "Controls", "Governance mapping", "Recommendation and pilot plan",
    ],
    minWords: 1500,
    maxWords: 2000,
  };
}

function course1(existing) {
  const sources = readSources();
  const base = existing.find((c) => c.id === "ai-agentic-insurance");
  if (!base) throw new Error("ai-agentic-insurance is not in the existing catalogue");
  const assessments = parseModuleAssessments();
  const modules = [];
  const used = new Set();
  const moduleTitles = {
    1: "Foundations: AI in Ugandan insurance",
    2: "Working with generative AI in daily insurance work",
    3: "Agentic AI: from assistants to agents",
    4: "Responsible AI governance in insurance",
  };

  for (const m of [1, 2, 3, 4]) {
    const text = readContent(`C01_M${m}_Research_Packs.md`);
    const units = text.split(/^## Unit /m).slice(1);
    const lessons = units.map((unit) => {
      const lines = unit.split("\n");
      const head = /^(\d\.\d)\s+—\s+(.+)$/.exec(lines[0]);
      if (!head) throw new Error(`Bad unit heading in module ${m}: ${lines[0]}`);
      const number = head[1];
      const title = clean(head[2]);
      const outcome = clean(/\*\*Learning outcome:\*\*\s*(.+)/.exec(unit)?.[1] ?? "");
      const minutes = /\*\*Video length target:\*\*\s*(\d+)/.exec(unit)?.[1];
      const sections = {};
      let current = null;
      for (const line of lines.slice(1)) {
        const h = /^### (.+)$/.exec(line);
        if (h) {
          current = h[1].trim();
          sections[current] = [];
        } else if (current) sections[current].push(line);
      }

      const blocks = [];
      for (const [name, label] of Object.entries(SECTION_TITLES)) {
        if (!sections[name]) continue;
        blocks.push({ type: "h", text: label });
        if (name === "Realistic Ugandan scenarios") blocks.push({ type: "note", text: "These scenarios are illustrative. They have not yet been checked against interviews with practitioners." });
        blocks.push(...blocksFrom(sections[name]));
      }

      const sourceIds = [...new Set((sections["Sources"] ?? []).join(" ").match(/S\d\d/g) ?? [])];
      sourceIds.forEach((id) => used.add(id));
      const questionKey = Object.keys(sections).find((k) => k.startsWith("Assessment questions"));
      const questions = parseUnitQuestions(sections[questionKey] ?? [], `u${number.replace(".", "-")}`);
      if (questions.length !== 10) throw new Error(`Unit ${number} has ${questions.length} questions, expected 10`);

      const readMinutes = Math.max(4, Math.round(blocks.reduce((sum, b) => sum + words(JSON.stringify(b)), 0) / 180));
      return {
        id: `unit-${number.replace(".", "-")}`,
        number,
        title,
        minutes: readMinutes,
        outcome,
        videoMinutes: minutes ? Number(minutes) : null,
        blocks,
        sources: sourceIds,
        tryIt: null,
        video: null,
        quiz: { id: `unit-${number.replace(".", "-")}-quiz`, title: `Unit ${number} quiz`, passMark: 70, maxAttempts: 0, questions },
      };
    });
    modules.push({
      id: `module-${m}`,
      title: moduleTitles[m],
      lessons,
      assessment: { id: `module-${m}-assessment`, title: `Module ${m} assessment`, passMark: 70, maxAttempts: 3, questions: assessments[String(m)] },
    });
  }

  const map = readContent("C01_00_Course_Map.md");
  const outcome = clean(/### Course outcome\s+([\s\S]*?)\n###/.exec(map)?.[1] ?? base.description);
  const finalTask = parseFinalTask();
  return {
    ...base,
    description: outcome,
    level: "Introductory",
    duration: "12 units in 4 modules, with a final applied task",
    who: [
      "Claims officers, underwriters, brokers, bancassurance relationship managers and customer service officers",
      "People with working knowledge of insurance operations and no technical AI background",
    ],
    outcomes: [
      "Identify where AI and agentic AI can add value in your own work",
      "Use generative AI tools safely and productively",
      "Verify AI output before relying on it",
      "Protect personal data in line with the Data Protection and Privacy Act 2019",
      "Assess an AI use case against recognised governance principles so it can be proposed responsibly",
    ],
    modules,
    check: null,
    finalTask,
    requirements: "Pass all twelve unit quizzes (70%), all four module assessments (70%, up to three attempts each) and the final applied task (60 of 100).",
    sources: [...used].sort().map((id) => sources[id]).filter(Boolean),
    contentStatus: "Pilot content. Written from desk research. Review by a subject expert and checks against practitioner interviews and the full text of the laws cited are still to be done.",
  };
}

// ---------------------------------------------------------------- Course 2 (map only)

function course2() {
  const text = readContent("C02_Course_Map.md");
  const outcome = clean(/### Course outcome\s+([\s\S]*?)\n###/.exec(text)?.[1] ?? "");
  const outline = [];
  let module = null;
  for (const line of text.split("\n")) {
    if (!line.trim().startsWith("|")) continue;
    const cells = line.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
    if (cells.length < 3 || /^-+$/.test(cells[0].replace(/\s/g, "")) || cells[0] === "Module") continue;
    if (cells[0]) module = clean(cells[0].replace(/\*\*/g, ""));
    if (cells[1]) outline.push({ module, unit: clean(cells[1]), outcome: clean(cells[2]) });
  }
  if (outline.length < 8) throw new Error("Course 2 outline was not read");
  const style = CATEGORY_STYLE["sales-experience"];
  return {
    id: "claims-communication",
    category: "sales-experience",
    title: "Claims Communication & Service Recovery",
    short: "Communicate claims processes, delays and decisions clearly, handle frustrated customers and recover from service failures.",
    description: outcome,
    audience: "Claims officers and customer service teams",
    duration: "5 modules, 11 units (planned)",
    level: null,
    hue: style.hue,
    icon: "headset",
    status: "coming_soon",
    outcomes: [],
    who: ["Claims officers and customer service teams"],
    iipAreas: [],
    modules: [],
    outline,
    check: null,
    finalTask: null,
    requirements: "",
    sources: [],
  };
}

// ---------------------------------------------------------------- build

const existing = existingCourses();
const catalogue = existing.map((c) => (c.id === "ai-agentic-insurance" ? course1(existing) : c));
catalogue.push(course2());
catalogue.forEach((c, i) => (c.number = i + 1));

const output = { generatedFrom: "docs/iip-academy/academy-content and lib/academy", courses: catalogue };
const json = JSON.stringify(output, null, 1);
if (json.includes(EM)) throw new Error("An em dash survived in the content");
if (/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(json)) throw new Error("An emoji survived in the content");

fs.mkdirSync(path.dirname(outFile), { recursive: true });
fs.writeFileSync(outFile, json + "\n", "utf8");
const lessons = catalogue.reduce((n, c) => n + c.modules.reduce((m, mod) => m + mod.lessons.length, 0), 0);
console.log(`Wrote ${path.relative(repo, outFile)}: ${catalogue.length} courses, ${lessons} lessons, ${(json.length / 1024).toFixed(0)} KB.`);
for (const c of catalogue) console.log(` ${c.number}. ${c.id} [${c.status}] ${c.modules.length} modules, ${c.modules.reduce((m, x) => m + x.lessons.length, 0)} lessons`);
