import Link from "next/link";
import { MarketingBrandLink } from "./MarketingNav";
import { MarketingContainer } from "./motion";

// Ported from CIP's MarketingFooter, with the legal pages (terms and privacy) linked in their own column.

export default function MarketingFooter() {
  return (
    <footer className="border-t border-[var(--mkt-border)] bg-white pb-[max(2rem,env(safe-area-inset-bottom))] pt-12">
      <MarketingContainer>
        <div className="grid gap-10 md:grid-cols-12">
          <div className="md:col-span-5">
            <MarketingBrandLink />
            <p className="m-0 mt-4 max-w-sm text-sm leading-6 text-[var(--mkt-muted)]">
              AI that prepares insurance work, for insurers and for banks that sell insurance. People make every decision.
            </p>
          </div>
          <div className="md:col-span-3">
            <h3 className="m-0 text-xs font-bold uppercase tracking-wider text-[var(--mkt-fg)]">Products</h3>
            <ul className="m-0 mt-3 list-none space-y-2 p-0 text-sm text-[var(--mkt-muted)]">
              <li><Link href="/signup?product=insurance" className="hover:text-[var(--mkt-brand)]">IIP for Insurance</Link></li>
              <li><Link href="/signup?product=bancassurance" className="hover:text-[var(--mkt-brand)]">IIP for Bancassurance</Link></li>
              <li><Link href="/#how-it-works" className="hover:text-[var(--mkt-brand)]">How it works</Link></li>
              <li><Link href="/#trust" className="hover:text-[var(--mkt-brand)]">Security</Link></li>
            </ul>
          </div>
          <div className="md:col-span-2">
            <h3 className="m-0 text-xs font-bold uppercase tracking-wider text-[var(--mkt-fg)]">Account</h3>
            <ul className="m-0 mt-3 list-none space-y-2 p-0 text-sm text-[var(--mkt-muted)]">
              <li><Link href="/login" className="hover:text-[var(--mkt-brand)]">Sign in</Link></li>
              <li><Link href="/signup" className="hover:text-[var(--mkt-brand)]">Sign up</Link></li>
            </ul>
          </div>
          <div className="md:col-span-2">
            <h3 className="m-0 text-xs font-bold uppercase tracking-wider text-[var(--mkt-fg)]">Legal</h3>
            <ul className="m-0 mt-3 list-none space-y-2 p-0 text-sm text-[var(--mkt-muted)]">
              <li><Link href="/privacy" className="hover:text-[var(--mkt-brand)]">Privacy Policy</Link></li>
              <li><Link href="/terms" className="hover:text-[var(--mkt-brand)]">Terms and Conditions</Link></li>
            </ul>
          </div>
        </div>
        <div className="mt-10 flex flex-col gap-3 border-t border-[var(--mkt-border)] pt-6 text-xs text-[var(--mkt-muted)] sm:flex-row sm:items-center sm:justify-between">
          <p className="m-0 flex items-center gap-2">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/otic-logo.png" alt="Otic" className="h-5 w-auto" />
            <span>&copy; {new Date().getFullYear()} Otic Group. All rights reserved.</span>
          </p>
          <a href="mailto:info@oticgroup.net" className="hover:text-[var(--mkt-brand)]">info@oticgroup.net</a>
        </div>
      </MarketingContainer>
    </footer>
  );
}
