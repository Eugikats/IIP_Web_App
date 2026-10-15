import type { Metadata } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import type { ReactNode } from "react";
import AcademyRoot from "@/components/academy/AcademyRoot";
import "@/components/academy/academy.css";
import "@/components/academy/academy-extra.css";

const jakarta = Plus_Jakarta_Sans({ subsets: ["latin"], weight: ["400", "500", "600", "700", "800"], variable: "--font-jakarta", display: "swap" });

export const metadata: Metadata = {
  title: "IIP Academy",
  description: "Practical skills for insurance professionals. Courses, certificates and a community, from IIP Academy.",
};

export default function AcademyLayout({ children }: { children: ReactNode }) {
  return <AcademyRoot fontClass={jakarta.variable}>{children}</AcademyRoot>;
}
