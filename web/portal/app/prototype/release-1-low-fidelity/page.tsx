import { notFound } from "next/navigation";
import Release1LowFidelityPrototype from "@/lovable-pages/Release1LowFidelityPrototype";

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ variant?: string }>;
}) {
  if (process.env.NODE_ENV === "production") {
    notFound();
  }

  const { variant = "A" } = await searchParams;
  return <Release1LowFidelityPrototype initialVariant={variant} />;
}
