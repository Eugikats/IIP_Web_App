import type { Preview } from "@storybook/nextjs";
import "../app/globals.css";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    a11y: {
      test: "todo"
    },
    controls: {
      expanded: true
    }
  },
  decorators: [
    (Story) => (
      <main className="min-h-screen bg-shell p-6 text-ink">
        <div className="mx-auto max-w-6xl">
          <Story />
        </div>
      </main>
    )
  ]
};

export default preview;
