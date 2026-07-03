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
      },
      boxShadow: {
        xs: "var(--shadow-xs)",
        sm: "var(--shadow-sm)",
        panel: "var(--shadow-panel)",
        card: "var(--shadow-card)",
        popover: "var(--shadow-popover)",
        drawer: "var(--shadow-drawer)",
        focus: "var(--shadow-focus)",
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
    },
  },
  plugins: [],
};
export default config;
