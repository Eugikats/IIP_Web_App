import type { Config } from "tailwindcss";

const token = (name: string) => `rgb(var(${name}) / <alpha-value>)`;

const config: Config = {
  content: [
    "./pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        bg: token("--color-bg"),
        shell: token("--color-shell"),
        surface: {
          DEFAULT: token("--color-surface"),
          soft: token("--color-surface-soft"),
          muted: token("--color-surface-muted"),
          raised: token("--color-surface-raised"),
        },
        backdrop: token("--color-backdrop"),
        line: {
          DEFAULT: token("--color-line"),
          soft: token("--color-line-soft"),
          strong: token("--color-line-strong"),
          focus: token("--color-line-focus"),
        },
        ink: {
          DEFAULT: token("--color-ink"),
          soft: token("--color-ink-soft"),
        },
        muted: {
          DEFAULT: token("--color-muted"),
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
          DEFAULT: token("--color-ai"),
          hover: token("--color-ai-hover"),
          soft: token("--color-ai-soft"),
          border: token("--color-ai-border"),
          surface: token("--color-surface-ai"),
          "surface-strong": token("--color-surface-ai-strong"),
        },
        success: {
          DEFAULT: token("--color-success"),
          soft: token("--color-success-soft"),
          border: token("--color-success-border"),
          text: token("--color-success-text"),
        },
        warning: {
          DEFAULT: token("--color-warning"),
          soft: token("--color-warning-soft"),
          border: token("--color-warning-border"),
          text: token("--color-warning-text"),
        },
        danger: {
          DEFAULT: token("--color-danger"),
          soft: token("--color-danger-soft"),
          border: token("--color-danger-border"),
          text: token("--color-danger-text"),
        },
        info: {
          DEFAULT: token("--color-info"),
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
          DEFAULT: token("--color-ai"),
          soft: token("--color-ai-soft"),
          border: token("--color-ai-border"),
        },
        mist: token("--color-surface-soft"),
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
      },
      backgroundImage: {
        "gradient-primary": "var(--gradient-primary)",
        "gradient-secondary": "var(--gradient-secondary)",
        "gradient-tertiary": "var(--gradient-tertiary)",
        "gradient-ambient": "var(--gradient-ambient)",
        "brand-gradient": "var(--gradient-primary)",
      },
      borderRadius: {
        xs: "var(--radius-xs)",
        sm: "var(--radius-sm)",
        md: "var(--radius-md)",
        lg: "var(--radius-lg)",
        xl: "var(--radius-xl)",
        "2xl": "var(--radius-2xl)",
        pill: "var(--radius-pill)",
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
      },
      animation: {
        "fade-in": "fade-in .3s ease-out",
        shimmer: "shimmer 2.4s linear infinite",
        "pulse-soft": "pulse-soft 2s ease-in-out infinite",
      },
    },
  },
  plugins: [],
};
export default config;
