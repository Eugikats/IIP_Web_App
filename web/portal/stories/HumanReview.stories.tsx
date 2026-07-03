import type { Meta, StoryObj } from "@storybook/nextjs";
import { ApprovalDecisionPanel } from "@/components/ApprovalDecisionPanel";

const meta = {
  title: "Governance/HumanReview",
  component: ApprovalDecisionPanel
} satisfies Meta<typeof ApprovalDecisionPanel>;

export default meta;
type Story = StoryObj<typeof meta>;

const recordDecision = async () => undefined;

export const AllDecisionsAvailable: Story = {
  args: {
    onDecision: recordDecision,
    allowedDecisions: {
      approve: true,
      reject: true,
      override: true,
      escalate: true
    }
  }
};

export const RestrictedByBackendPermission: Story = {
  args: {
    onDecision: recordDecision,
    allowedDecisions: {
      approve: false,
      reject: false,
      override: false,
      escalate: true
    }
  }
};

export const DisabledDuringReview: Story = {
  args: {
    onDecision: recordDecision,
    disabled: true,
    allowedDecisions: {
      approve: true,
      reject: true,
      override: true,
      escalate: true
    }
  }
};
