import { VerifyPage } from "@/components/academy/pages/LearnerPages";

export default async function Page({ params }: { params: Promise<{ code?: string[] }> }) {
  const { code } = await params;
  return <VerifyPage code={code?.[0] ?? null} />;
}
