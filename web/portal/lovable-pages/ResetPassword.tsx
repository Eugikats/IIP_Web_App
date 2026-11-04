"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame, FormError, FormNotice, TextField } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { resetPassword } from "@/lib/platformAuth";

export default function ResetPassword() {
  const token = useSearchParams().get("token") ?? "";
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const result = await resetPassword(token, password);
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setNotice(result.value.message);
  }

  return (
    <AuthFrame title="Choose a new password">
      {notice ? (
        <>
          <FormNotice message={notice} testId="reset-done" />
          <p className="mt-6"><Link to="/signin" className="text-sm font-semibold text-dls-primary underline">Sign in</Link></p>
        </>
      ) : (
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <TextField
            id="reset-password" label="New password" value={password} onChange={setPassword} type="password" autoComplete="new-password"
            hint="At least 12 characters. A few unrelated words make a strong, memorable password."
          />
          <FormError message={error} />
          <Button variant="primary" type="submit" disabled={busy || !token || password.length === 0}>{busy ? "Saving..." : "Change password"}</Button>
          {!token ? <p className="text-xs text-dls-danger-text">This link is missing its code. Use the link in the email.</p> : null}
        </form>
      )}
    </AuthFrame>
  );
}
