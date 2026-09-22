import { Suspense } from "react";
import AIConversations from "@/lovable-pages/AIConversations";

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return (
    <Suspense fallback={null}>
      <AIConversations conversationId={id} />
    </Suspense>
  );
}
