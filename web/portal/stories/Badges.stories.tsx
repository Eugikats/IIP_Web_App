import type { Meta, StoryObj } from "@storybook/nextjs";
import { RiskBadge, SlaBadge, StatusBadge } from "@/components/Badges";

const meta = {
  title: "Governance/Badges"
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

const statuses = ["Connected", "Healthy", "Not configured", "Not ready", "Pending approval", "Failed"];
const risks = ["Low", "Medium", "High", "Critical"];
const slas = ["OnTrack", "AtRisk", "Breached"];

export const StatusMatrix: Story = {
  render: () => (
    <div className="space-y-6 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 shadow-dls-none">
      <section>
        <h2 className="text-[15px] font-bold text-dls-text-primary">Provider and workflow status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {statuses.map((status) => (
            <StatusBadge key={status} label={status} />
          ))}
        </div>
      </section>
      <section>
        <h2 className="text-[15px] font-bold text-dls-text-primary">Risk status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {risks.map((risk) => (
            <RiskBadge key={risk} label={risk} />
          ))}
        </div>
      </section>
      <section>
        <h2 className="text-[15px] font-bold text-dls-text-primary">SLA status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {slas.map((sla) => (
            <SlaBadge key={sla} label={sla} />
          ))}
        </div>
      </section>
    </div>
  )
};
