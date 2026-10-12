// How the Academy's courses are grouped so a person can start broad and go deeper: a category, then its courses,
// then a course's lessons. A course belongs to the category named on the course itself; the order of the courses
// inside a category is the order they are listed in courses.ts. A category is only shown when it holds a course
// that exists, so nothing here is a placeholder for a course that has not been defined.

import { COURSES, isReady, type Course } from "@/lib/academy/courses";

export type AcademyCategory = {
  id: string;
  title: string;
  summary: string;
};

const ALL: AcademyCategory[] = [
  {
    id: "ai-foundations",
    title: "AI foundations",
    summary: "What AI, generative AI and agentic AI are, where they fit in insurance, and how a person stays in charge.",
  },
  {
    id: "claims-fraud",
    title: "Claims and fraud",
    summary: "From first notice to payment: intake, documents, assessment, fraud signals, service standards and customer updates.",
  },
  {
    id: "underwriting-risk",
    title: "Underwriting and risk",
    summary: "Prepare applications, profile risk, inform pricing, read the portfolio and support the underwriter's decision.",
  },
  {
    id: "sales-experience",
    title: "Sales and customer experience",
    summary: "Win, serve and keep customers on WhatsApp and digital channels, with consent and a person ready to take over.",
  },
  {
    id: "build-govern",
    title: "Build and govern",
    summary: "Build agentic systems for insurance, and govern them: oversight, explanation, audit and data protection.",
  },
];

export const coursesIn = (categoryId: string): Course[] => COURSES.filter((course) => course.category === categoryId);

/** The categories that hold at least one course. */
export const CATEGORIES: AcademyCategory[] = ALL.filter((category) => coursesIn(category.id).length > 0);

export const getCategory = (id: string | null | undefined) => CATEGORIES.find((category) => category.id === id) ?? null;

export const categoryOfCourse = (course: Course) => CATEGORIES.find((category) => category.id === course.category) ?? null;

/** How many courses in a category can be taken now. */
export const readyCount = (categoryId: string) => coursesIn(categoryId).filter(isReady).length;
