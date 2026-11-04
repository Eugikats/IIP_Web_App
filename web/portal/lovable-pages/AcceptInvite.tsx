"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame, FormError, FormNotice, TextField } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { acceptInvitation, previewInvitation, type InvitationPreview } from "@/lib/platformAuth";

/** Joining a workspace from an emailed invitation: a new person chooses a name and password; someone who already signs in proves it. */
export default function AcceptInvite() {
  const token = useSearchParams().get("token") ?? "";
  const [preview, setPreview] = useState<InvitationPreview | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [joined, setJoined] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setLoadError("This link is missing its code. Use the link in the invitation email.");
      return;
    }

    void previewInvitation(token).then((result) => (result.ok ? setPreview(result.value) : setLoadError(result.error)));
  }, [token]);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const result = await acceptInvitation({ token, name, password });
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setJoined(result.value.message);
  }

  if (loadError) {
    return (
      <AuthFrame title="This invitation cannot be used">
        <FormError message={loadError} />
        <p className="mt-4 text-sm text-dls-text-secondary">It may have expired, been used, or been withdrawn. Ask the person who invited you to send a new one.</p>
      </AuthFrame>
    );
  }

  if (!preview) {
    return <AuthFrame title="Checking your invitation"><p className="text-sm text-dls-text-secondary">One moment.</p></AuthFrame>;
  }

  if (joined) {
    return (
      <AuthFrame title={`You have joined ${preview.workspaceName}`}>
        <FormNotice message={joined} testId="invite-joined" />
        <p className="mt-6"><Link to="/signin" className="text-sm font-semibold text-dls-primary underline">Sign in</Link></p>
      </AuthFrame>
    );
  }

  return (
    <AuthFrame
      title={`Join ${preview.workspaceName}`}
      intro={`You have been invited as ${preview.role} using ${preview.email}. ${preview.hasAccount ? "You already have a sign-in, so confirm it with your password." : "Choose a name and a password to finish."}`}
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {!preview.hasAccount && <TextField id="invite-name" label="Your name" value={name} onChange={setName} autoComplete="name" />}
        <TextField
          id="invite-password" label={preview.hasAccount ? "Your password" : "Choose a password"} value={password} onChange={setPassword} type="password"
          autoComplete={preview.hasAccount ? "current-password" : "new-password"}
          hint={preview.hasAccount ? undefined : "At least 12 characters. A few unrelated words make a strong, memorable password."}
        />
        <FormError message={error} />
        <Button variant="primary" type="submit" disabled={busy || password.length === 0}>{busy ? "Joining..." : "Join the workspace"}</Button>
      </form>
    </AuthFrame>
  );
}
