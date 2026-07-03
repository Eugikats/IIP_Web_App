import type { Meta, StoryObj } from "@storybook/nextjs";
import { MetricCard } from "@/components/MetricCard";

const meta = {
  title: "Governance/MetricCard",
  component: MetricCard
} satisfies Meta<typeof MetricCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ToneMatrix: Story = {
  args: {
    label: "Story-only metric",
    value: "0"
  },
  render: () => (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <MetricCard label="Open work items" value="42" detail="Backend-owned count for the active workspace." />
      <MetricCard label="At risk" value="7" detail="Requires human review before any consequential action." tone="warning" />
      <MetricCard label="Breached" value="3" detail="Escalation required by the configured workflow." tone="danger" />
      <MetricCard label="Provider health" value="5/5" detail="No story fixture status may be copied into runtime routes." tone="success" />
    </div>
  )
};

export const LongContent: Story = {
  args: {
    label: "Very long backend-owned metric label that should wrap cleanly",
    value: "123,456,789",
    detail: "Long supporting text remains within the card without resizing neighboring metric cards or overlapping action surfaces.",
    eyebrow: "Story-only fixture"
  }
};
