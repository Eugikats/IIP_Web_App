import type { Meta, StoryObj } from "@storybook/nextjs";
import { DataTable } from "@/components/DataTable";
import { StatusBadge } from "@/components/Badges";

const meta = {
  title: "Governance/DataTable",
  component: DataTable,
} satisfies Meta<typeof DataTable>;

export default meta;
type Story = StoryObj<typeof meta>;

const rows = [
  { reference: "CLM-2026-0184", customer: "Grace Nakato", status: "Pending", amount: "UGX 4,250,000" },
  { reference: "CLM-2026-0177", customer: "Moses Okwir", status: "Approved", amount: "UGX 890,000" },
  { reference: "CLM-2026-0165", customer: "Amina Nabatanzi", status: "Rejected", amount: "UGX 12,000,000" },
];

export const Populated: Story = {
  args: { children: null },
  render: () => (
    <DataTable label="Story-only claims fixture">
      <thead>
        <tr className="border-b border-dls-border-subtle">
          <th className="px-2 py-2.5 text-left text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">Reference</th>
          <th className="px-2 py-2.5 text-left text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">Customer</th>
          <th className="px-2 py-2.5 text-left text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">Status</th>
          <th className="px-2 py-2.5 text-right text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">Amount</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.reference} className="border-b border-dls-border-subtle last:border-0 hover:bg-dls-bg-page">
            <td className="px-2 py-3 font-semibold text-dls-primary">{row.reference}</td>
            <td className="px-2 py-3 text-dls-text-primary">{row.customer}</td>
            <td className="px-2 py-3"><StatusBadge label={row.status} /></td>
            <td className="px-2 py-3 text-right tabular-nums text-dls-text-primary">{row.amount}</td>
          </tr>
        ))}
      </tbody>
    </DataTable>
  ),
};

export const Empty: Story = {
  args: { children: null },
  render: () => (
    <DataTable label="Story-only empty fixture">
      <thead>
        <tr className="border-b border-dls-border-subtle">
          <th className="px-2 py-2.5 text-left text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">Reference</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td className="px-2 py-6 text-center text-[13.5px] text-dls-text-secondary">No rows to display.</td>
        </tr>
      </tbody>
    </DataTable>
  ),
};
