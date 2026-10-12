"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ArrowLeft, ChevronRight, Layers } from "lucide-react";
import { CATEGORIES, coursesIn, getCategory, type AcademyCategory } from "@/lib/academy/categories";

// Choose a category, see the courses in it, open a course to see its lessons. The chosen category lives in the
// address (?category=claims-fraud), so the back button and a shared link both land where the person was.

/** The category chosen in the address, or null for "everything". */
export function useChosenCategory(): AcademyCategory | null {
  const params = useSearchParams();
  return getCategory(params?.get("category"));
}

export function CategoryTiles({ basePath }: { basePath: string }) {
  const chosen = useChosenCategory();
  return (
    <section aria-label="Course categories" data-testid="academy-categories">
      <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">Browse by category</h2>
      <ul className="m-0 mt-3 grid list-none gap-3 p-0 sm:grid-cols-2 xl:grid-cols-3">
        {CATEGORIES.map((category) => {
          const active = chosen?.id === category.id;
          const count = coursesIn(category.id).length;
          return (
            <li key={category.id}>
              <Link
                href={`${basePath}?category=${category.id}`}
                data-testid={`category-${category.id}`}
                aria-current={active ? "true" : undefined}
                className={`group flex h-full flex-col rounded-xl border p-4 transition-colors ${active ? "border-[#1769FF] bg-[#F5F8FF]" : "border-slate-200/90 bg-white hover:border-[#1769FF]/50 hover:bg-[#F8FAFF]"}`}
              >
                <span className="flex items-start justify-between gap-3">
                  <span className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-blue-50 text-[#1769FF]"><Layers className="h-4 w-4" aria-hidden /></span>
                  <ChevronRight className="h-4 w-4 text-slate-300 transition-transform group-hover:translate-x-0.5 group-hover:text-[#1769FF]" aria-hidden />
                </span>
                <span className="mt-3 text-[15px] font-bold text-[#0B1640]">{category.title}</span>
                <span className="mt-1 text-[12.5px] leading-5 text-slate-600">{category.summary}</span>
                <span className="mt-3 text-[11.5px] font-semibold uppercase tracking-wide text-slate-400">
                  {count} {count === 1 ? "course" : "courses"}
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/** Shown above the courses when a category is chosen: where you are, what it is, and the way back. */
export function CategoryHeader({ basePath }: { basePath: string }) {
  const chosen = useChosenCategory();
  if (!chosen) return null;
  return (
    <div data-testid="category-header" className="rounded-xl border border-[#1769FF]/25 bg-[#F5F8FF] p-4">
      <nav aria-label="Where you are" className="flex flex-wrap items-center gap-1.5 text-[12px] text-slate-500">
        <Link href={basePath} className="inline-flex items-center gap-1 font-semibold text-[#1769FF] hover:underline">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden /> All courses
        </Link>
        <span aria-hidden>/</span>
        <span className="font-semibold text-[#0B1640]">{chosen.title}</span>
      </nav>
      <p className="m-0 mt-2 text-[13px] leading-5 text-slate-600">{chosen.summary}</p>
    </div>
  );
}

/** The courses to show: the chosen category's, or all of them. */
export function filterByCategory<T extends { category: string }>(courses: T[], chosen: AcademyCategory | null): T[] {
  return chosen ? courses.filter((course) => course.category === chosen.id) : courses;
}
