import type { Meta, StoryObj } from "@storybook/nextjs";
import { useState } from "react";
import { SideDrawer } from "@/components/ui/SideDrawer";
import { Button } from "@/components/ui/Button";

const meta = {
  title: "Governance/SideDrawer",
  component: SideDrawer,
} satisfies Meta<typeof SideDrawer>;

export default meta;
type Story = StoryObj<typeof meta>;

function Demo({ description, footer }: { description?: string; footer?: boolean }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="p-6">
      <p className="mb-4 max-w-prose text-[14px] leading-[1.6] text-dls-text-secondary">
        The page behind stays visible and inert while the drawer is open, so the decision being
        supported is never lost. Escape closes it and focus returns to the trigger.
      </p>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        Open evidence
      </Button>
      <SideDrawer
        open={open}
        onClose={() => setOpen(false)}
        title="Police abstract"
        description={description}
        footer={footer ? <Button variant="secondary" onClick={() => setOpen(false)}>Close</Button> : undefined}
      >
        <div className="space-y-4 text-[14px] leading-[1.6] text-dls-text-primary">
          <p>
            Source document opened from a citation chip. In the product this scrolls to and
            highlights the region the assertion was drawn from.
          </p>
          <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface-sunken p-4 text-[13px] text-dls-text-secondary">
            Document preview region
          </div>
        </div>
      </SideDrawer>
    </div>
  );
}

export const Default: Story = {
  args: { open: false, title: "Police abstract", children: null, onClose: () => {} },
  render: () => <Demo />,
};

export const WithDescription: Story = {
  args: { open: false, title: "Police abstract", children: null, onClose: () => {} },
  render: () => <Demo description="Received 2026-09-04 by WhatsApp · page 2 of 3" />,
};

export const WithFooter: Story = {
  args: { open: false, title: "Police abstract", children: null, onClose: () => {} },
  render: () => <Demo description="Received 2026-09-04 by WhatsApp · page 2 of 3" footer />,
};
