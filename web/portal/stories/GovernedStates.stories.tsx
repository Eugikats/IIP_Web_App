import type { Meta, StoryObj } from "@storybook/nextjs";
import {
  EmptyFilteredState,
  EmptyState,
  ErrorState,
  LoadingState,
  NotConfiguredState,
  UnauthorizedState,
} from "@/components/States";

const meta = {
  title: "Governance/States",
  parameters: {
    docs: {
      description: {
        component:
          "Design language v2 §8.24 — all six states a data surface must implement: loading, empty, empty-after-filter, error, not configured, no permission."
      }
    }
  }
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const AllStates: Story = {
  render: () => (
    <div className="grid gap-4 md:grid-cols-2">
      <LoadingState label="Loading governed workspace data…" />
      <EmptyState
        title="No work items available"
        description="This view has no backend-returned records for the current tenant, workspace, role, and filters."
      />
      <EmptyFilteredState onClear={() => {}} />
      <ErrorState
        message="The backend could not load this workspace view."
        onRetry={() => {}}
        correlationId="corr-story-fixture-001"
      />
      <NotConfiguredState what="An administrator must connect a document intelligence provider before extraction can run." />
      <UnauthorizedState permission="Claims.ViewAssigned" />
    </div>
  )
};
