import type { Meta, StoryObj } from "@storybook/nextjs";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/Button";

const meta = {
  title: "Governance/PageHeader",
  component: PageHeader,
} satisfies Meta<typeof PageHeader>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithBreadcrumbAndAction: Story = {
  args: { title: "CLM-2026-0184" },
  render: () => (
    <PageHeader
      breadcrumb={[{ label: "Claims", href: "/app/claims" }, { label: "CLM-2026-0184" }]}
      eyebrow="Motor · Own damage"
      title="CLM-2026-0184"
      description="Grace Nakato · reported 2026-09-04 · assigned to you"
      actions={<Button variant="primary">Review claim</Button>}
    />
  ),
};

export const TitleOnly: Story = {
  args: { title: "Settings" },
  render: () => <PageHeader title="Settings" />,
};

export const LongTitleWraps: Story = {
  args: { title: "A very long page title that must still read as engineered rather than default when it wraps" },
  render: () => (
    <PageHeader
      title="A very long page title that must still read as engineered rather than default when it wraps"
      description="Long one-line descriptions still sit on a single line at the intended max width."
    />
  ),
};
