import type { Meta, StoryObj } from "@storybook/nextjs";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/Button";

const meta = {
  title: "Primitives/Button",
  component: Button,
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="primary" icon={<Plus />}>Create claim</Button>
      <Button variant="secondary">Cancel</Button>
      <Button variant="ghost">Dismiss</Button>
      <Button variant="destructive">Revoke session</Button>
    </div>
  ),
};

export const Disabled: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="primary" disabled>
        Approve claim
      </Button>
      <p className="text-[13px] text-dls-text-muted">Disabled stays violet, it reads as &quot;not yet&quot;, never &quot;broken&quot;.</p>
    </div>
  ),
};
