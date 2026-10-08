import type { Metadata } from "next";
import { Suspense } from "react";
import SignUpChooser from "@/components/marketing/SignUpChooser";

export const metadata: Metadata = {
  title: { absolute: "Sign up | Insurance Intelligence Platform" },
  description: "Choose IIP for Insurance or IIP for Bancassurance and create your account.",
};

export default function SignUpPage() {
  return (
    <Suspense fallback={null}>
      <SignUpChooser />
    </Suspense>
  );
}
