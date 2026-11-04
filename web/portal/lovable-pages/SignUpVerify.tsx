"use client";

import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Loader2 } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame, FormError, FormNotice } from "@/components/iip/AuthFrame";
import { verifySignUp } from "@/lib/platformAuth";

/** The page an email's confirmation link opens: the address is proved and the workspace is made. */
export default function SignUpVerify() {
  const token = useSearchParams().get("token");
  const attempted = useRef(false);
  const [state, setState] = useState<{ kind: "working" } | { kind: "done"; message: string } | { kind: "failed"; message: string }>({ kind: "working" });

  useEffect(() => {
    if (attempted.current) return;
    attempted.current = true;
    if (!token) {
      setState({ kind: "failed", message: "This link is missing its code. Use the link in the email, or start again." });
      return;
    }

    void verifySignUp(token).then((result) =>
      setState(result.ok ? { kind: "done", message: result.value.message } : { kind: "failed", message: result.error }));
  }, [token]);

  return (
    <AuthFrame title={state.kind === "done" ? "Your workspace is ready" : "Confirming your email"}>
      {state.kind === "working" && (
        <p className="flex items-center gap-2 text-sm text-dls-text-secondary"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> One moment.</p>
      )}
      {state.kind === "done" && (
        <>
          <FormNotice message={state.message} testId="signup-verified" />
          <p className="mt-6"><Link to="/signin" className="text-sm font-semibold text-dls-primary underline">Sign in</Link></p>
        </>
      )}
      {state.kind === "failed" && (
        <>
          <FormError message={state.message} />
          <p className="mt-6"><Link to="/signup" className="text-sm font-semibold text-dls-primary underline">Start again</Link></p>
        </>
      )}
    </AuthFrame>
  );
}
