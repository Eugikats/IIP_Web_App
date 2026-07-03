import type { Meta, StoryObj } from "@storybook/nextjs";
import { CustomerUpdatePanel, type CustomerUpdate } from "@/components/CustomerUpdatePanel";

const meta = {
  title: "Governance/CustomerUpdates",
  component: CustomerUpdatePanel
} satisfies Meta<typeof CustomerUpdatePanel>;

export default meta;
type Story = StoryObj<typeof meta>;

const updates: CustomerUpdate[] = [
  {
    updateId: "story-update-001",
    reasonCategory: "Missing documents",
    channel: "Email",
    status: "PendingApproval",
    messageBody:
      "We need one more approved document before the insurer can continue the review. This is story-only copy and not runtime customer data.",
    requiresApproval: true,
    deliveryStatus: null,
    updatedAt: "2026-06-19T09:05:00Z",
    allowedActions: [
      { action: "approveCustomerUpdate", label: "Approve update", requiredPermission: "ManageCustomerCommunications", reason: null },
      { action: "rejectCustomerUpdate", label: "Reject update", requiredPermission: "ManageCustomerCommunications", reason: null }
    ],
    blockedActions: [
      { action: "sendCustomerUpdate", label: "Send through configured provider", requiredPermission: "SendCustomerUpdate", reason: "This customer update requires human approval before sending." }
    ]
  },
  {
    updateId: "story-update-002",
    reasonCategory: "Status update",
    channel: "Portal",
    status: "Approved",
    messageBody:
      "The case remains under review. A human reviewer will send any final customer communication through a configured provider.",
    requiresApproval: false,
    deliveryStatus: "Queued",
    updatedAt: "2026-06-19T09:20:00Z",
    allowedActions: [
      { action: "sendCustomerUpdate", label: "Send through configured provider", requiredPermission: "SendCustomerUpdate", reason: null }
    ],
    blockedActions: [
      { action: "approveCustomerUpdate", label: "Approve update", requiredPermission: "ManageCustomerCommunications", reason: "Only updates pending human approval can be approved." },
      { action: "rejectCustomerUpdate", label: "Reject update", requiredPermission: "ManageCustomerCommunications", reason: "Only updates pending human approval can be rejected." }
    ]
  }
];

export const WithGovernedActions: Story = {
  args: {
    updates,
    onApprove: async () => undefined,
    onSend: async () => undefined
  }
};

export const ReadOnly: Story = {
  args: {
    updates
  }
};

export const Empty: Story = {
  args: {
    updates: []
  }
};
