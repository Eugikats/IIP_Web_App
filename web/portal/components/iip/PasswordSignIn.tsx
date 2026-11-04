"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { FormError, TextField } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { signIn, type WorkspaceChoice } from "@/lib/platformAuth";

/** Email and password sign-in for companies that do not use Microsoft 365, offered next to Entra. */
export function PasswordSignIn({ onSignedIn, allowSignUp }: { onSignedIn: () => void; allowSignUp: boolean }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [choices, setChoices] = useState<WorkspaceChoice[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(workspace?: WorkspaceChoice) {
    setBusy(true);
    setError(null);
    const result = await signIn(email, password, workspace);
    setBusy(false);
    if (!result.ok) return setError(result.error);
    if ("choose" in result.value) return setChoices(result.value.choose);
    onSignedIn();
  }

  return (
    <div className="mt-8 border-t border-dls-border-subtle pt-6" data-testid="password-sign-in">
      <h3 className="text-sm font-semibold text-dls-text-primary">Sign in with email</h3>
      {choices ? (
        <div className="mt-3 space-y-2" role="group" aria-label="Choose a workspace">
          <p className="text-xs text-dls-text-muted">Your email belongs to more than one workspace. Choose one.</p>
          {choices.map((choice) => (
            <Button key={`${choice.tenantId}/${choice.workspaceId}`} variant="secondary" disabled={busy} onClick={() => void submit(choice)}>
              {choice.name}
            </Button>
          ))}
        </div>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
          noValidate
          className="mt-3 space-y-4"
        >
          <TextField id="signin-email" label="Email" value={email} onChange={setEmail} type="email" autoComplete="username" />
          <TextField id="signin-password" label="Password" value={password} onChange={setPassword} type="password" autoComplete="current-password" />
          <Button
            variant="primary"
            type="submit"
            disabled={busy || email.trim().length === 0 || password.length === 0}
            icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
          >
            {busy ? "Signing in..." : "Sign in"}
          </Button>
        </form>
      )}
      <FormError message={error} />
      <p className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-xs text-dls-text-muted">
        <Link to="/forgot-password" className="font-semibold text-dls-primary underline">Forgotten your password?</Link>
        {allowSignUp ? <Link to="/signup" className="font-semibold text-dls-primary underline">Open a workspace for your company</Link> : null}
      </p>
    </div>
  );
}
