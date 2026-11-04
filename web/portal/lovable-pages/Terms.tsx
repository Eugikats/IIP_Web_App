"use client";

import { Link } from "@/components/iip/NextRouterCompat";
import { AuthFrame } from "@/components/iip/AuthFrame";

/**
 * Plain-language draft of the terms a company accepts when it opens a workspace.
 * It is a working draft: the wording must be reviewed and approved by legal counsel before it is relied on.
 */
export default function Terms() {
  return (
    <AuthFrame title="Terms of use and privacy notice" intro="Draft wording, to be confirmed by legal. Do not rely on it as a final agreement.">
      <div className="space-y-5 text-sm leading-6 text-dls-text-secondary" data-testid="terms-draft">
        <section>
          <h2 className="text-base font-semibold text-dls-text-primary">What you are opening</h2>
          <p>A workspace on Insurance Intelligence Platform (IIP) for your company. The person who signs up becomes its administrator and can invite colleagues and choose what each of them may do.</p>
        </section>
        <section>
          <h2 className="text-base font-semibold text-dls-text-primary">Who decides</h2>
          <p>IIP prepares work: summaries, extracted fields, drafts and suggestions. People at your company make every decision about claims, payments, policies and what is sent to customers. IIP does not decide for you.</p>
        </section>
        <section>
          <h2 className="text-base font-semibold text-dls-text-primary">Your data</h2>
          <p>Your workspace is separate from every other company&apos;s. We use your claims, documents and messages to run your workspace and for no other company. You can ask for records to be exported or erased, subject to the retention periods your company sets and the law requires.</p>
        </section>
        <section>
          <h2 className="text-base font-semibold text-dls-text-primary">Your account</h2>
          <p>We keep your name, work email and a one-way fingerprint of your password. We never see or store the password itself. Keep it private and tell us if you think someone else has it.</p>
        </section>
        <section>
          <h2 className="text-base font-semibold text-dls-text-primary">Acceptable use</h2>
          <p>Use IIP for lawful insurance operations. Do not try to reach another company&apos;s data, interfere with the service, or use it to make automated final decisions about people.</p>
        </section>
        <p className="text-xs text-dls-text-muted">This page is a draft prepared for review. It has not been approved by legal counsel.</p>
        <p><Link to="/signup" className="font-semibold text-dls-primary underline">Back to sign-up</Link></p>
      </div>
    </AuthFrame>
  );
}
