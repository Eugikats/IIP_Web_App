import type { Meta, StoryObj } from "@storybook/nextjs";
import { EmptyState, ErrorState, LoadingState, UnauthorizedState } from "@/components/States";

const meta = {
  title: "Governance/States",
  parameters: {
    docs: {
      description: {
        component: "Story-only fixtures for approved empty, loading, error, and backend-permission states."
      }
    }
  }
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const AllStates: Story = {
  render: () => (
    <div className="grid gap-4 md:grid-cols-2">
      <EmptyState
        title="No work items available"
        description="This view has no backend-returned records for the current tenant, workspace, role, and filters."
      />
      <LoadingState label="Loading governed workspace data..." />
      <ErrorState message="The backend could not load this workspace view. Retry or contact support if the issue continues." />
      <UnauthorizedState permission="Claims.ViewAssigned" />
    </div>
  )
};
