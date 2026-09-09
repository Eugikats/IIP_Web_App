import type { Meta, StoryObj } from "@storybook/nextjs";
import { useState } from "react";
import { FilterTab } from "@/components/ui/FilterTab";

const meta = {
  title: "Governance/FilterTab",
  component: FilterTab,
} satisfies Meta<typeof FilterTab>;

export default meta;
type Story = StoryObj<typeof meta>;

const tabs = ["All", "WhatsApp", "Email", "Web", "SMS"] as const;

function Demo() {
  const [active, setActive] = useState<(typeof tabs)[number]>("All");
  const counts: Record<(typeof tabs)[number], number> = { All: 17, WhatsApp: 6, Email: 4, Web: 5, SMS: 2 };
  return (
    <div className="flex flex-wrap gap-1.5">
      {tabs.map((t) => (
        <FilterTab key={t} active={active === t} onClick={() => setActive(t)} count={counts[t]}>
          {t}
        </FilterTab>
      ))}
    </div>
  );
}

export const Row: Story = {
  args: { active: false, onClick: () => {}, children: "All" },
  render: () => <Demo />,
};

export const DisabledZeroCount: Story = {
  args: { active: false, onClick: () => {}, children: "Escalated", count: 0, disabled: true },
};
