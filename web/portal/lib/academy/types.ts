// Shapes the Academy API returns. Public course views never carry lesson text or answers.

export type Me =
  | { signedIn: false }
  | { signedIn: true; userId: string; name: string; email: string | null; hasOrganisation: boolean; isOperator: boolean };

export type CourseSummary = {
  id: string;
  number: number;
  category: string;
  title: string;
  short: string;
  level: string | null;
  duration: string;
  hue: [string, string];
  icon: string;
  status: "ready" | "coming_soon";
  modules: number;
  lessons: number;
  lessonIds: string[];
  minutes: number;
  price: number;
  currency: string;
  hasCertificate: boolean;
};

export type CurriculumLesson = { id: string; number: string | null; title: string; minutes: number; hasQuiz: boolean };

export type CurriculumModule = {
  id: string;
  title: string;
  lessons: CurriculumLesson[];
  assessment: { id: string; title: string; questions: number; passMark: number; maxAttempts: number } | null;
};

export type CourseDetail = CourseSummary & {
  description: string;
  audience: string;
  outcomes: string[];
  who: string[];
  iipAreas: string[];
  requirements: string;
  contentStatus: string | null;
  modules: CurriculumModule[];
  check: { id: string; title: string; questions: number; passMark: number } | null;
  finalTask: { id: string; title: string; passMark: number; outOf: number } | null;
  outline: { module: string | null; unit: string; outcome: string }[];
  sources: { id: string; title: string; type: string; date: string; status: string }[];
};

export type Block =
  | { type: "p"; text: string }
  | { type: "h"; text: string }
  | { type: "note"; text: string }
  | { type: "list"; items: string[]; ordered?: boolean }
  | { type: "table"; head: string[]; rows: string[][] }
  | { type: "flow"; steps: string[]; note: string | null }
  | { type: "screen"; file: string; caption: string; callouts: string[] }
  | { type: "screenMissing"; text: string }
  | { type: "example"; title: string; steps: string[] }
  | { type: "takeaways"; items: string[] };

export type LessonView = {
  id: string;
  courseId: string;
  moduleId: string;
  moduleTitle: string;
  moduleNumber: number;
  indexInModule: number;
  lessonsInModule: number;
  number: string | null;
  title: string;
  minutes: number;
  outcome: string;
  blocks: Block[];
  sources: string[];
  tryIt: string | null;
  video: string | null;
  note: string;
  hasQuiz: boolean;
  done: boolean;
};

export type CategoryDef = { id: string; title: string };

export type LessonProgressState = { doneLessons: string[]; passedQuizzes: string[]; lastLessonId: string | null };

export type AssessmentRecord = { attempts: number; best: number; passed: boolean };

export type FinalTaskState = { status: "none" | "submitted" | "passed" | "not_passed"; score: number | null; feedback: string | null; submittedAt: string | null };

export type CourseProgress = LessonProgressState & {
  assessments: Record<string, AssessmentRecord>;
  checkPassed: boolean;
  finalTask: FinalTaskState;
};

export type CertificateView = { id: string; code: string; courseId: string; courseTitle: string; name: string; issuedAt: string };

export type NotificationView = { id: string; text: string; href: string; icon: string; read: boolean; createdAt: string };

export type ProfileView = {
  name: string;
  jobTitle: string;
  organisation: string;
  about: string;
  language: string;
  goal: string;
  emailNotifications: boolean;
  weeklySummary: boolean;
  replies: boolean;
  aiSuggestions: boolean;
  communityConsent: boolean;
};

export type LearnerState = {
  profile: ProfileView;
  enrolled: { courseId: string; enrolledAt: string }[];
  progress: Record<string, CourseProgress>;
  certificates: CertificateView[];
  notifications: NotificationView[];
  stats: { streakDays: number; minutesWeek: number[]; todayIndex: number; minutesToday: number };
  activity: { text: string; at: string }[];
  groups: string[];
};

export const EMPTY_PROGRESS: CourseProgress = {
  doneLessons: [], passedQuizzes: [], lastLessonId: null, assessments: {}, checkPassed: false,
  finalTask: { status: "none", score: null, feedback: null, submittedAt: null },
};
