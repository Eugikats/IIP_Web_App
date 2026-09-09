import type { Meta, StoryObj } from "@storybook/nextjs";
import { SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";

const meta = {
  title: "Governance/SectionHeader",
  component: SectionHeader,
} satisfies Meta<typeof SectionHeader>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithBreadcrumbAndAction: Story = {
  args: { title: "CLM-2026-0184" },
  render: () => (
    <SectionHeader
      breadcrumb={[{ label: "Claims", href: "/app/claims" }, { label: "CLM-2026-0184" }]}
      eyebrow="Motor · Own damage"
      title="CLM-2026-0184"
      sub="Grace Nakato · reported 2026-09-04 · assigned to you"
      action={<Button variant="primary">Review claim</Button>}
    />
  ),
};

export const TitleOnly: Story = {
  args: { title: "Settings" },
  render: () => <SectionHeader title="Settings" />,
};

export const EyebrowNoBreadcrumb: Story = {
  args: { title: "Document library" },
  render: () => (
    <SectionHeader
      eyebrow="Documents"
      title="Document library"
      sub="All documents ingested across claims, customers, and channels."
    />
  ),
};
