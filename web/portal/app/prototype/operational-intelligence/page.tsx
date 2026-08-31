import { notFound } from "next/navigation";
import OperationalIntelligencePrototype from "@/lovable-pages/OperationalIntelligencePrototype";

export default function Page() {
  if (process.env.NODE_ENV === "production") {
    notFound();
  }

  return <OperationalIntelligencePrototype />;
}
