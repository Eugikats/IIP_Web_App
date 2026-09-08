import type { Meta, StoryObj } from "@storybook/nextjs";
import { SectionCard } from "@/components/SectionCard";
import { Button } from "@/components/ui/Button";

const meta = {
  title: "Governance/SectionCard",
  component: SectionCard,
} satisfies Meta<typeof SectionCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithHeader: Story = {
  args: { children: null },
  render: () => (
    <SectionCard
      eyebrow="Story-only fixture"
      title="Claim evidence"
      description="Documents received against this claim's checklist."
      actions={<Button variant="secondary">Upload document</Button>}
    >
      <p className="text-[13.5px] text-dls-text-secondary">Card body content goes here.</p>
    </SectionCard>
  ),
};

export const HeaderlessContentOnly: Story = {
  args: { children: null },
  render: () => (
    <SectionCard>
      <p className="text-[13.5px] text-dls-text-secondary">
        A distinct object or action wraps in a card; a plain content group does not need a header row.
      </p>
    </SectionCard>
  ),
};

export const TitleOnlyNoActions: Story = {
  args: { children: null },
  render: () => (
    <SectionCard title="Policy summary">
      <p className="text-[13.5px] text-dls-text-secondary">Motor comprehensive, active since 2026-01-14.</p>
    </SectionCard>
  ),
};
