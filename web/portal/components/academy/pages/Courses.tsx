"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { CourseCard } from "@/components/academy/CourseCard";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { categoryTitle } from "@/lib/academy/categoryTitles";

const FILTERS: [string, string][] = [
  ["all", "All courses"], ["mine", "My courses"], ["active", "In progress"], ["notstarted", "Not started"], ["completed", "Completed"], ["explore", "Explore new"],
];

// All courses, as designed: a search box, the six filters and the grid. A filter that depends on the person's progress
// is shown to everyone but, for a visitor, simply has nothing in it.
export default function Courses() {
  const params = useSearchParams();
  const show = params.get("show") ?? "all";
  const [query, setQuery] = useState("");
  const { courses, coursesError, isEnrolled, statusOf, me } = useAcademy();

  if (coursesError) return <PageState kind="error" title="The courses could not be loaded" text={coursesError} />;
  if (!courses) return <SkeletonCards count={6} />;

  const q = query.trim().toLowerCase();
  const list = courses.filter((course) => {
    const status = statusOf(course);
    if (show === "mine" && !(isEnrolled(course.id) && status !== "Completed")) return false;
    if (show === "active" && status !== "In progress") return false;
    if (show === "notstarted" && status !== "Not started") return false;
    if (show === "completed" && status !== "Completed") return false;
    if (show === "explore" && isEnrolled(course.id)) return false;
    return !q || `${course.title} ${categoryTitle(course.category)}`.toLowerCase().includes(q);
  });

  return (
    <>
      <div className="pagehead">
        <h1>All courses</h1>
        <p>{courses.length} courses built for insurance professionals. Learn at your own pace and earn certificates.</p>
      </div>
      <div className="toolbar">
        <label className="search">
          <Icon name="search" size={18} />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search courses or topics" aria-label="Search courses or topics" />
        </label>
        <div className="chips">
          {FILTERS.map(([id, label]) => (
            <Link key={id} className={`chip${show === id ? " on" : ""}`} href={id === "all" ? "/academy/courses" : `/academy/courses?show=${id}`}>{label}</Link>
          ))}
        </div>
      </div>
      <div className="grid3" id="cgridList">
        {list.length ? list.map((course) => <CourseCard key={course.id} course={course} />) : (
          <div className="empty" style={{ gridColumn: "1/-1" }} data-testid="courses-empty">
            {q ? `No courses match "${query}".` : me?.signedIn || show === "explore" || show === "all" ? "No courses match. Try another filter." : "Sign in to see your own courses here."}
          </div>
        )}
      </div>
    </>
  );
}
