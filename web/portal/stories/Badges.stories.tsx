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
    <div className="space-y-6 rounded-2xl border border-line bg-surface p-6 shadow-panel">
      <section>
        <h2 className="text-sm font-semibold text-ink">Provider and workflow status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {statuses.map((status) => (
            <StatusBadge key={status} label={status} />
          ))}
        </div>
      </section>
      <section>
        <h2 className="text-sm font-semibold text-ink">Risk status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {risks.map((risk) => (
            <RiskBadge key={risk} label={risk} />
          ))}
        </div>
      </section>
      <section>
        <h2 className="text-sm font-semibold text-ink">SLA status</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          {slas.map((sla) => (
            <SlaBadge key={sla} label={sla} />
          ))}
        </div>
      </section>
    </div>
  )
};
