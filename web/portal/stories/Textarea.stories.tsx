import type { Meta, StoryObj } from "@storybook/nextjs";
import { Textarea } from "@/components/ui/Input";
import { Field } from "@/components/ui/Input";

const meta = {
  title: "Governance/Textarea",
  component: Textarea,
} satisfies Meta<typeof Textarea>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { placeholder: "Write only customer-safe content…", rows: 4 },
};

export const WithField: Story = {
  args: { placeholder: "Write only customer-safe content…" },
  render: () => (
    <Field label="Incident description *" htmlFor="story-textarea">
      <Textarea id="story-textarea" placeholder="What happened, in the claimant's own words…" />
    </Field>
  ),
};
