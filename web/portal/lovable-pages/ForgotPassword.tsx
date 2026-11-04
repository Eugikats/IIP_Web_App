"use client";

import { useState } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame, FormError, FormNotice, TextField } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { requestPasswordReset } from "@/lib/platformAuth";

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const result = await requestPasswordReset(email);
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setNotice(result.value.message);
  }

  return (
    <AuthFrame title="Forgotten your password?" intro="Enter the email address you sign in with. If it has an account, we will send a link to choose a new password.">
      {notice ? (
        <FormNotice message={notice} testId="forgot-sent" />
      ) : (
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <TextField id="forgot-email" label="Email" value={email} onChange={setEmail} type="email" autoComplete="email" />
          <FormError message={error} />
          <Button variant="primary" type="submit" disabled={busy || email.trim().length === 0}>{busy ? "Sending..." : "Send the link"}</Button>
        </form>
      )}
      <p className="mt-6"><Link to="/signin" className="text-sm font-semibold text-dls-primary underline">Back to sign-in</Link></p>
    </AuthFrame>
  );
}
