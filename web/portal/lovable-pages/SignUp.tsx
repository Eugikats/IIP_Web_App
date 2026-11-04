"use client";

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame, FormError, FormNotice, TextField } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { loadCapabilities, signUp, type AuthCapabilities } from "@/lib/platformAuth";

/** Open a workspace for a company: details, a confirmation email, and nothing is created until the address is proved. */
export default function SignUp() {
  const [capabilities, setCapabilities] = useState<AuthCapabilities | null>(null);
  const [company, setCompany] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [terms, setTerms] = useState(false);
  const [website, setWebsite] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fields, setFields] = useState<Record<string, string>>({});
  const [sent, setSent] = useState<string | null>(null);

  useEffect(() => {
    void loadCapabilities().then(setCapabilities);
  }, []);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setFields({});
    const result = await signUp({ companyName: company, adminName: name, email, password, acceptTerms: terms, website });
    setBusy(false);
    if (!result.ok) {
      setError(result.fields ? "Please correct the highlighted details." : result.error);
      setFields(result.fields ?? {});
      return;
    }

    setSent(result.value.message ?? "Check your email. We sent a link to confirm your address.");
  }

  if (capabilities && !capabilities.signUp) {
    return (
      <AuthFrame title="Sign-up is not open here" intro="This IIP deployment is not open to new companies. If your company already uses IIP, ask your administrator to invite you.">
        <Link to="/signin" className="text-sm font-semibold text-dls-primary underline">Go to sign-in</Link>
      </AuthFrame>
    );
  }

  if (sent) {
    return (
      <AuthFrame title="Check your email" intro="We sent a link to the address you gave. Open it to confirm that it is yours; your workspace is created at that moment.">
        <FormNotice message={sent} testId="signup-sent" />
        <p className="mt-4 text-xs text-dls-text-muted">The link works once and expires in 24 hours. Nothing has been created yet.</p>
      </AuthFrame>
    );
  }

  return (
    <AuthFrame title="Open your claims workspace" intro="Tell us about your company and who will administer it. You will confirm your email next; no IIP staff need to be involved.">
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <TextField id="signup-company" label="Company name" value={company} onChange={setCompany} autoComplete="organization" error={fields.companyName} />
        <TextField id="signup-name" label="Your name" value={name} onChange={setName} autoComplete="name" error={fields.adminName} />
        <TextField id="signup-email" label="Work email" value={email} onChange={setEmail} type="email" autoComplete="email" error={fields.email} />
        <TextField
          id="signup-password" label="Password" value={password} onChange={setPassword} type="password" autoComplete="new-password"
          hint="At least 12 characters. A few unrelated words make a strong, memorable password." error={fields.password}
        />
        {/* A field only software fills in. People never see it, so anything typed here is a robot. */}
        <div aria-hidden="true" style={{ position: "absolute", left: "-10000px", width: 1, height: 1, overflow: "hidden" }}>
          <label htmlFor="signup-website">Website</label>
          <input id="signup-website" tabIndex={-1} autoComplete="off" value={website} onChange={(event) => setWebsite(event.target.value)} />
        </div>
        <label className="flex items-start gap-2 text-sm text-dls-text-primary">
          <input type="checkbox" className="mt-1 h-4 w-4" checked={terms} onChange={(event) => setTerms(event.target.checked)} />
          <span>I agree to the <Link to="/terms" className="font-semibold underline">terms of use and privacy notice</Link>.</span>
        </label>
        {fields.acceptTerms ? <p className="text-[11.5px] text-dls-danger-text">{fields.acceptTerms}</p> : null}
        <FormError message={error} />
        <Button variant="primary" type="submit" disabled={busy} icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>
          {busy ? "Sending the confirmation..." : "Create workspace"}
        </Button>
      </form>
      <p className="mt-6 text-sm text-dls-text-secondary">
        Already have a workspace? <Link to="/signin" className="font-semibold text-dls-primary underline">Sign in</Link>
      </p>
    </AuthFrame>
  );
}
