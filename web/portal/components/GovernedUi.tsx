import { ButtonHTMLAttributes, ReactNode } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  FileText,
  Info,
  Loader2,
  ShieldCheck,
  Sparkles,
  XCircle
} from "lucide-react";

type ButtonVariant = "primary" | "secondary" | "tertiary" | "ai" | "destructive" | "authority";

type ButtonProps = {
  children: ReactNode;
  variant?: ButtonVariant;
  icon?: ReactNode;
  disabledReason?: string;
} & ButtonHTMLAttributes<HTMLButtonElement>;

const buttonClass: Record<ButtonVariant, string> = {
  primary: "border-transparent bg-gradient-primary text-white shadow-glow-cyan hover:opacity-90",
  secondary: "border-line bg-surface text-ink hover:border-violet/40 hover:bg-surface-soft",
  tertiary: "border-transparent bg-transparent text-muted hover:bg-surface-soft hover:text-ink",
  ai: "border-violet/30 bg-violet/10 text-violet hover:bg-violet hover:text-white",
  destructive: "border-danger/30 bg-danger/10 text-danger hover:bg-danger hover:text-white",
  authority: "border-success/30 bg-success/10 text-success hover:bg-success hover:text-white"
};

export function GovernedButton({ children, variant = "secondary", icon, disabledReason, disabled, className = "", ...props }: ButtonProps) {
  const isDisabled = disabled || Boolean(disabledReason);

  return (
    <span className="inline-flex flex-col items-start gap-1">
      <button
        {...props}
        disabled={isDisabled}
        aria-disabled={isDisabled}
        title={disabledReason ?? props.title}
        className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border px-4 py-2 text-sm font-semibold shadow-panel transition-colors disabled:cursor-not-allowed disabled:border-line disabled:bg-surface-muted disabled:text-muted-soft ${buttonClass[variant]} ${className}`}
      >
        {icon ? <span className="flex h-4 w-4 items-center justify-center" aria-hidden="true">{icon}</span> : null}
        <span>{children}</span>
      </button>
      {disabledReason ? <span className="max-w-xs text-xs leading-5 text-muted">{disabledReason}</span> : null}
    </span>
  );
}

type StatusTone = "neutral" | "success" | "warning" | "danger" | "info" | "ai";

const statusToneClass: Record<StatusTone, string> = {
  neutral: "border-line bg-surface-muted text-muted",
  success: "border-success/30 bg-success/10 text-success",
  warning: "border-warning/30 bg-warning/10 text-warning",
  danger: "border-danger/30 bg-danger/10 text-danger",
  info: "border-info/30 bg-info/10 text-info",
  ai: "border-violet/30 bg-violet/10 text-violet"
};

const StatusIcon: Record<StatusTone, typeof Info> = {
  neutral: Info,
  success: CheckCircle2,
  warning: AlertTriangle,
  danger: XCircle,
  info: Clock3,
  ai: Sparkles
};

export function StatusChip({ label, tone = "neutral" }: { label: string; tone?: StatusTone }) {
  const Icon = StatusIcon[tone];

  return (
    <span className={`inline-flex max-w-full items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold leading-tight ${statusToneClass[tone]}`}>
      <Icon className="h-3.5 w-3.5 flex-shrink-0" aria-hidden="true" />
      <span className="min-w-0 break-words">{label}</span>
    </span>
  );
}

export type BlockedAction = {
  action: string;
  reasonCode: string;
  message: string;
};

export function BlockedActionPanel({ title = "Action blocked", actions }: { title?: string; actions: BlockedAction[] }) {
  if (actions.length === 0) return null;

  return (
    <section className="rounded-2xl border border-warning/30 bg-warning/10 p-4">
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 flex-shrink-0 text-warning" aria-hidden="true" />
        <div className="min-w-0">
          <h3 className="text-sm font-semibold text-ink">{title}</h3>
          <div className="mt-3 space-y-2">
            {actions.map((action) => (
              <div key={`${action.action}-${action.reasonCode}`} className="rounded-xl border border-line bg-surface px-3 py-2">
                <p className="font-mono text-xs font-semibold text-muted">{action.action} / {action.reasonCode}</p>
                <p className="mt-1 text-sm leading-6 text-ink">{action.message}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

export function ConfirmationAuditNotice({
  action,
  affectedObject,
  impact,
  children
}: {
  action: string;
  affectedObject: string;
  impact: string;
  children?: ReactNode;
}) {
  return (
    <section className="rounded-2xl border border-line bg-surface p-4 shadow-panel">
      <div className="flex items-start gap-3">
        <ShieldCheck className="mt-0.5 h-5 w-5 flex-shrink-0 text-violet" aria-hidden="true" />
        <div className="min-w-0">
          <h3 className="text-sm font-semibold text-ink">{action}</h3>
          <p className="mt-1 text-sm text-muted">{affectedObject}</p>
          <p className="mt-3 rounded-xl border border-line bg-surface-soft px-3 py-2 text-sm leading-6 text-ink">{impact}</p>
          <p className="mt-3 text-xs leading-5 text-muted">A mandatory reason and audit event are required before this governed action is recorded.</p>
          {children ? <div className="mt-4">{children}</div> : null}
        </div>
      </div>
    </section>
  );
}

export function ProcessingStageList({ stages }: { stages: Array<{ label: string; state: "pending" | "running" | "complete" | "failed" }> }) {
  return (
    <ol className="space-y-2">
      {stages.map((stage) => {
        const Icon = stage.state === "running" ? Loader2 : stage.state === "complete" ? CheckCircle2 : stage.state === "failed" ? XCircle : Clock3;
        const tone = stage.state === "complete" ? "text-success" : stage.state === "failed" ? "text-danger" : stage.state === "running" ? "text-violet" : "text-muted-soft";
        return (
          <li key={stage.label} className="flex items-center gap-2 text-sm text-muted">
            <Icon className={`h-4 w-4 flex-shrink-0 ${tone} ${stage.state === "running" ? "animate-spin" : ""}`} aria-hidden="true" />
            <span>{stage.label}</span>
          </li>
        );
      })}
    </ol>
  );
}

export function AiInsightCard({ title, summary, sources }: { title: string; summary: string; sources?: string[] }) {
  return (
    <article className="rounded-2xl border border-violet/20 bg-surface-ai p-4 shadow-panel">
      <div className="flex items-start gap-3">
        <Sparkles className="mt-0.5 h-5 w-5 flex-shrink-0 text-violet" aria-hidden="true" />
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.12em] text-violet">AI finding</p>
          <h3 className="mt-1 text-sm font-semibold text-ink">{title}</h3>
          <p className="mt-2 text-sm leading-6 text-muted">{summary}</p>
          {sources && sources.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-2">
              {sources.map((source) => (
                <span key={source} className="inline-flex items-center gap-1.5 rounded-full border border-line bg-surface px-2.5 py-1 text-xs text-muted">
                  <FileText className="h-3.5 w-3.5" aria-hidden="true" />
                  {source}
                </span>
              ))}
            </div>
          ) : null}
        </div>
      </div>
    </article>
  );
}

export function PaginationControls({
  page,
  totalPages,
  onPrevious,
  onNext
}: {
  page: number;
  totalPages: number;
  onPrevious?: () => void;
  onNext?: () => void;
}) {
  return (
    <nav className="flex items-center gap-2" aria-label="Pagination">
      <GovernedButton variant="tertiary" icon={<ChevronLeft className="h-4 w-4" />} disabled={page <= 1} onClick={onPrevious}>
        Previous
      </GovernedButton>
      <span className="font-mono text-xs font-semibold text-muted">Page {page} of {totalPages}</span>
      <GovernedButton variant="tertiary" icon={<ChevronRight className="h-4 w-4" />} disabled={page >= totalPages} onClick={onNext}>
        Next
      </GovernedButton>
    </nav>
  );
}
