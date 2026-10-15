"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { academyGet, AcademyApiError } from "@/lib/academy/api";
import { EMPTY_PROGRESS, type CourseProgress, type CourseSummary, type LearnerState, type Me } from "@/lib/academy/types";
import { Icon } from "@/components/academy/Icon";
import { canUseLocalTestDoubles } from "@/lib/config";
import { startSignIn } from "@/lib/entraAuth";

// Everything the Academy's pages share: who is here, the catalogue, what this learner has done, and the toast and
// dialog the design shows over any page. Pages ask this for state and never fetch it themselves.

type ModalState = { content: ReactNode; className: string } | null;

type AcademyContextValue = {
  me: Me | null;
  loadingMe: boolean;
  courses: CourseSummary[] | null;
  coursesError: string | null;
  learner: LearnerState | null;
  loadingLearner: boolean;
  refreshLearner: () => Promise<void>;
  setLearner: (next: LearnerState) => void;
  courseById: (id: string) => CourseSummary | undefined;
  isEnrolled: (courseId: string) => boolean;
  progressOf: (courseId: string) => CourseProgress;
  percentOf: (course: CourseSummary) => number;
  statusOf: (course: CourseSummary) => "Completed" | "In progress" | "Not started" | null;
  nextLessonId: (course: CourseSummary) => string | null;
  hasCertificate: (courseId: string) => boolean;
  toast: (text: string, icon?: string) => void;
  showModal: (content: ReactNode, className?: string) => void;
  closeModal: () => void;
  zenithOpen: boolean;
  setZenithOpen: (open: boolean) => void;
  /** Sends a visitor to Microsoft and brings them back to this page. */
  signIn: () => void;
};

const AcademyContext = createContext<AcademyContextValue | null>(null);

export function useAcademy(): AcademyContextValue {
  const value = useContext(AcademyContext);
  if (!value) throw new Error("useAcademy must be used inside AcademyProvider");
  return value;
}

export function AcademyProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null);
  const [loadingMe, setLoadingMe] = useState(true);
  const [courses, setCourses] = useState<CourseSummary[] | null>(null);
  const [coursesError, setCoursesError] = useState<string | null>(null);
  const [learner, setLearnerState] = useState<LearnerState | null>(null);
  const [loadingLearner, setLoadingLearner] = useState(false);
  const [toastState, setToastState] = useState<{ text: string; icon: string; show: boolean }>({ text: "", icon: "check", show: false });
  const [modal, setModal] = useState<ModalState>(null);
  const [zenithOpen, setZenithOpen] = useState(false);
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    let cancelled = false;
    academyGet<Me>("/me")
      .then((result) => { if (!cancelled) setMe(result); })
      .catch(() => { if (!cancelled) setMe({ signedIn: false }); })
      .finally(() => { if (!cancelled) setLoadingMe(false); });
    academyGet<{ courses: CourseSummary[] }>("/public/courses")
      .then((result) => { if (!cancelled) setCourses(result.courses); })
      .catch((failure) => { if (!cancelled) setCoursesError(failure instanceof AcademyApiError ? failure.message : "The courses could not be loaded."); });
    return () => { cancelled = true; };
  }, []);

  const refreshLearner = useCallback(async () => {
    if (!me || !me.signedIn) { setLearnerState(null); return; }
    setLoadingLearner(true);
    try {
      setLearnerState(await academyGet<LearnerState>("/learner"));
    } catch {
      setLearnerState(null);
    } finally {
      setLoadingLearner(false);
    }
  }, [me]);

  useEffect(() => { void refreshLearner(); }, [refreshLearner]);

  const toast = useCallback((text: string, icon = "check") => {
    setToastState({ text, icon, show: true });
    if (toastTimer.current) clearTimeout(toastTimer.current);
    toastTimer.current = setTimeout(() => setToastState((current) => ({ ...current, show: false })), 2400);
  }, []);

  const showModal = useCallback((content: ReactNode, className = "") => setModal({ content, className }), []);
  const closeModal = useCallback(() => setModal(null), []);

  const value = useMemo<AcademyContextValue>(() => {
    const courseById = (id: string) => courses?.find((course) => course.id === id);
    const isEnrolled = (courseId: string) => Boolean(learner?.enrolled.some((entry) => entry.courseId === courseId));
    const progressOf = (courseId: string): CourseProgress => learner?.progress[courseId] ?? EMPTY_PROGRESS;
    const percentOf = (course: CourseSummary) => (course.lessons ? Math.round((progressOf(course.id).doneLessons.length / course.lessons) * 100) : 0);
    const hasCertificate = (courseId: string) => Boolean(learner?.certificates.some((entry) => entry.courseId === courseId));
    const statusOf = (course: CourseSummary) => {
      if (!isEnrolled(course.id)) return null;
      if (hasCertificate(course.id)) return "Completed" as const;
      return percentOf(course) > 0 ? ("In progress" as const) : ("Not started" as const);
    };
    const nextLessonId = (course: CourseSummary) => {
      const done = new Set(progressOf(course.id).doneLessons);
      return course.lessonIds.find((id) => !done.has(id)) ?? course.lessonIds[0] ?? null;
    };
    const signIn = () => {
      if (canUseLocalTestDoubles) return;
      void startSignIn("microsoft", window.location.pathname + window.location.search);
    };
    return {
      signIn,
      me, loadingMe, courses, coursesError, learner, loadingLearner, refreshLearner, setLearner: setLearnerState, courseById, isEnrolled, progressOf,
      percentOf, statusOf, nextLessonId, hasCertificate, toast, showModal, closeModal, zenithOpen, setZenithOpen,
    };
  }, [me, loadingMe, courses, coursesError, learner, loadingLearner, refreshLearner, toast, showModal, closeModal, zenithOpen]);

  return (
    <AcademyContext.Provider value={value}>
      {children}
      <div className={`toast${toastState.show ? " show" : ""}`} id="toast" role="status" aria-live="polite">
        <Icon name={toastState.icon} size={18} />
        {toastState.text}
      </div>
      <div
        className={`modal${modal ? " show" : ""}`}
        id="modal"
        onClick={(event) => { if (event.target === event.currentTarget) closeModal(); }}
      >
        <div className={`mbox ${modal?.className ?? ""}`.trim()} id="mbox" role="dialog" aria-modal="true">{modal?.content}</div>
      </div>
    </AcademyContext.Provider>
  );
}
