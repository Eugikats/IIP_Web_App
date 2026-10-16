import { CertificatePage } from "@/components/academy/pages/LearnerPages";

export default async function Page({ params }: { params: Promise<{ certificateId: string }> }) {
  const { certificateId } = await params;
  return <CertificatePage certificateId={certificateId} />;
}
