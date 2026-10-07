import type { Config } from "tailwindcss";

const token = (name: string) => `rgb(var(${name}) / <alpha-value>)`;
const hslToken = (name: string) => `hsl(var(${name}) / <alpha-value>)`;

const config: Config = {
  content: [
    "./pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
    "./lovable-pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./context/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        bg: token("--color-bg"),
        background: hslToken("--background"),
        foreground: hslToken("--foreground"),
        border: hslToken("--border"),
        input: hslToken("--input"),
        ring: hslToken("--ring"),
        shell: token("--color-shell"),
        surface: {
          DEFAULT: token("--color-surface"),
          soft: token("--color-surface-soft"),
          muted: token("--color-surface-muted"),
          raised: token("--color-surface-raised"),
        },
        backdrop: token("--color-backdrop"),
        // `line`.DEFAULT, `ink`, `muted`.DEFAULT and `danger`/`violet`/`ai`.DEFAULT below are
        // repointed to the literal design-system CSS custom properties (hslToken) rather than
        // the semantic RGB ones (token), so that consolidating the ~1,150 literal-token class
        // usages (navy/slate2/stone/cloud/violet2/error) onto these semantic names is a
        // zero-visual-change rename for the majority case. See WO-20 ledger entry for the
        // full rationale and the resulting color shift on the minority of usages that were
        // already semantic before this migration.
        line: {
          DEFAULT: hslToken("--cloud"),
          soft: token("--color-line-soft"),
          strong: token("--color-line-strong"),
          focus: token("--color-line-focus"),
        },
        ink: {
          DEFAULT: hslToken("--navy"),
          soft: hslToken("--slate"),
        },
        muted: {
          DEFAULT: hslToken("--stone"),
          soft: token("--color-muted-soft"),
        },
        disabled: token("--color-disabled"),
        inverse: token("--color-inverse"),
        primary: {
          DEFAULT: token("--color-primary"),
          hover: token("--color-primary-hover"),
          soft: token("--color-primary-soft"),
          border: token("--color-primary-border"),
        },
        ai: {
          DEFAULT: hslToken("--violet"),
          hover: token("--color-ai-hover"),
          soft: token("--color-ai-soft"),
          border: token("--color-ai-border"),
          surface: token("--color-surface-ai"),
          "surface-strong": token("--color-surface-ai-strong"),
        },
        success: {
          DEFAULT: hslToken("--success"),
          soft: token("--color-success-soft"),
          border: token("--color-success-border"),
          text: token("--color-success-text"),
        },
        warning: {
          DEFAULT: hslToken("--warning"),
          soft: token("--color-warning-soft"),
          border: token("--color-warning-border"),
          text: token("--color-warning-text"),
        },
        danger: {
          DEFAULT: hslToken("--error"),
          soft: token("--color-danger-soft"),
          border: token("--color-danger-border"),
          text: token("--color-danger-text"),
        },
        info: {
          DEFAULT: hslToken("--info"),
          soft: token("--color-info-soft"),
          border: token("--color-info-border"),
          text: token("--color-info-text"),
        },
        // Brand accent family. These names are referenced widely across the
        // component set (text-accent, border-accent, bg-accent, bg-brand-gradient,
        // text-violet, bg-mist). They map onto the Ambient Intelligence palette:
        // `accent` = primary blue action, `violet` = AI accent, `mist` = soft surface.
        accent: {
          DEFAULT: token("--color-primary"),
          soft: token("--color-primary-soft"),
          border: token("--color-primary-border"),
        },
        brand: {
          600: token("--color-primary-hover"),
        },
        violet: {
          DEFAULT: hslToken("--violet"),
          soft: token("--color-ai-soft"),
          border: token("--color-ai-border"),
        },
        mist: token("--color-surface-soft"),

        // ---- Design language v2 (docs/03-design-architecture/iip_design_language_v2.md §4.1) ----
        // A distinct namespace so nothing above is renamed or removed (migration map step 1: "keep
        // every existing token name"). New components/ui/ primitives and the app shell (AppShell,
        // Sidebar, TopBar) consume only these.
        dls: {
          primary: token("--color-primary"),
          "primary-hover": token("--color-primary-hover"),
          "primary-active": token("--color-primary-active"),
          "primary-accent": token("--color-primary-accent"),
          "primary-soft": token("--color-primary-soft"),
          "primary-soft-alt": token("--color-primary-soft-alt"),
          "primary-border": token("--color-primary-border"),
          "primary-idle": token("--color-primary-idle"),
          "text-primary": token("--color-text-primary"),
          "text-secondary": token("--color-text-secondary"),
          "text-muted": token("--color-text-muted"),
          "text-faint": token("--color-text-faint"),
          "text-inverse": token("--color-text-inverse"),
          "border-subtle": token("--color-border-subtle"),
          "border-input": token("--color-border-input"),
          "border-pill": token("--color-border-pill"),
          "border-strong": token("--color-border-strong"),
          "bg-page": token("--color-bg-page"),
          surface: token("--color-surface"),
          "surface-sunken": token("--color-surface-sunken"),
          "danger-dot": token("--color-danger-dot"),
          "danger-text": token("--color-danger-text"),
          "danger-tint": token("--color-danger-tint"),
          "warning-dot": token("--color-warning-dot"),
          "warning-text": token("--color-warning-text"),
          "warning-tint": token("--color-warning-tint"),
          "success-dot": token("--color-success-dot"),
          "success-text": token("--color-success-text"),
          "success-tint": token("--color-success-tint"),
          "info-dot": token("--color-info-dot"),
          "info-text": token("--color-info-text"),
          "info-tint": token("--color-info-tint"),
          scrim: token("--color-scrim"),
          avatar: {
            "1": token("--color-avatar-1"),
            "2": token("--color-avatar-2"),
            "3": token("--color-avatar-3"),
            "4": token("--color-avatar-4"),
            "5": token("--color-avatar-5"),
            "6": token("--color-avatar-6"),
            "7": token("--color-avatar-7"),
            "8": token("--color-avatar-8"),
          },
        },
      },
      boxShadow: {
        xs: "var(--shadow-xs)",
        sm: "var(--shadow-sm)",
        panel: "var(--shadow-panel)",
        card: "var(--shadow-card)",
        popover: "var(--shadow-popover)",
        drawer: "var(--shadow-drawer)",
        focus: "var(--shadow-focus)",
        l1: "var(--shadow-l1)",
        l2: "var(--shadow-l2)",
        l3: "var(--shadow-l3)",
        l4: "var(--shadow-l4)",
        glass: "var(--shadow-glass)",
        "glow-cyan": "0 8px 24px rgba(37, 99, 255, 0.14)",
        "glow-danger": "0 8px 24px rgba(220, 38, 38, 0.12)",
        "glow-success": "0 8px 24px rgba(5, 150, 105, 0.12)",
        "glow-warning": "0 8px 24px rgba(217, 119, 6, 0.12)",
        // Design language §6.3, the only three shadows a new component may use.
        "dls-popover": "var(--shadow-popover)",
        "dls-modal": "var(--shadow-drawer)",
        "dls-primary": "0 1px 2px rgb(23 105 255 / 0.25)",
        "dls-none": "none",
      },
      backgroundImage: {
        "gradient-primary": "var(--gradient-primary)",
        "gradient-secondary": "var(--gradient-secondary)",
        "gradient-tertiary": "var(--gradient-tertiary)",
        "gradient-ambient": "var(--gradient-ambient)",
        "brand-gradient": "var(--gradient-primary)",
        // Design language §4.5, the only two permitted gradients, nowhere else.
        "dls-orb": "var(--gradient-orb)",
        "dls-chart-bar": "var(--gradient-chart-bar)",
      },
      borderRadius: {
        xs: "var(--radius-xs)",
        sm: "var(--radius-sm)",
        md: "var(--radius-md)",
        lg: "var(--radius-lg)",
        xl: "var(--radius-xl)",
        "2xl": "var(--radius-2xl)",
        pill: "var(--radius-pill)",
        // Design language §6.2, nothing above 16px on any operational surface.
        "dls-tight": "var(--radius-tight)",
        "dls-control": "var(--radius-control)",
        "dls-chip": "var(--radius-chip)",
        "dls-card": "var(--radius-card)",
        "dls-overlay": "var(--radius-overlay)",
        "dls-pill": "var(--radius-pill)",
      },
      transitionDuration: {
        "dls-fast": "120ms",
        "dls-base": "160ms",
        "dls-exit": "100ms",
        // A panel changing width travels ~176px, far further than a colour change. At the
        // 120ms "fast" token that reads as a snap rather than a movement, so the rail gets
        // its own slightly longer step.
        "dls-panel": "220ms",
      },
      transitionTimingFunction: {
        dls: "cubic-bezier(0.2, 0, 0, 1)",
      },
      spacing: {
        "density-p": "var(--density-default-p)",
        "density-gap": "var(--density-default-gap)",
      },
      keyframes: {
        "fade-in": {
          from: { opacity: "0", transform: "translateY(4px)" },
          to: { opacity: "1", transform: "translateY(0)" },
        },
        shimmer: {
          "0%": { backgroundPosition: "-200% 0" },
          "100%": { backgroundPosition: "200% 0" },
        },
        "pulse-soft": {
          "0%,100%": { opacity: "1" },
          "50%": { opacity: ".6" },
        },
        // Labels appearing as the rail widens. Horizontal, because the panel edge is moving
        // horizontally -- the vertical "fade-in" above would have the text arrive across the
        // motion rather than with it.
        "label-in": {
          from: { opacity: "0", transform: "translateX(-4px)" },
          to: { opacity: "1", transform: "translateX(0)" },
        },
      },
      animation: {
        "fade-in": "fade-in .3s ease-out",
        shimmer: "shimmer 2.4s linear infinite",
        "pulse-soft": "pulse-soft 2s ease-in-out infinite",
        // Slightly shorter than the rail's own 220ms and eased out, so the text settles just
        // after the edge stops rather than racing it.
        "label-in": "label-in 180ms cubic-bezier(0.2, 0, 0, 1) both",
      },
    },
  },
  plugins: [],
};
export default config;
