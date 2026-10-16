"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { AcademyProvider, useAcademy } from "@/components/academy/AcademyProvider";
import { Icon } from "@/components/academy/Icon";
import { Avatar } from "@/components/academy/Avatar";
import ZenithWidget from "@/components/academy/ZenithWidget";
import { signOut } from "@/lib/entraAuth";

// The Academy's page frame, as in the design: a sticky top bar with the logo, four links, a bell and the person's menu;
// a bottom bar on a phone; Zenith's button and panel; the toast and the dialog. It has its own look and its own
// navigation, and it does not need the person to belong to an organisation.

const NAV: [string, string, string, string][] = [
  ["dashboard", "home", "Dashboard", "/academy"],
  ["courses", "book", "All courses", "/academy/courses"],
  ["community", "users", "Community", "/academy/community"],
  ["assistant", "sparkle", "Zenith", "/academy/zenith"],
];

export function routeOf(pathname: string): string {
  const parts = pathname.replace(/^\/academy\/?/, "").split("/").filter(Boolean);
  const first = parts[0] ?? "dashboard";
  switch (first) {
    case "courses": return parts[1] ? "course" : "courses";
    case "learn": case "assessment": case "final": return "learn";
    case "zenith": return "assistant";
    case "certificates": return parts[1] ? "certificate" : "certificates";
    case "signed-out": return "signin";
    case "admin": return "admin";
    case "verify": return "verify";
    default: return first;
  }
}

const navKey = (route: string) => (route === "course" || route === "learn" ? "courses" : route);

export default function AcademyRoot({ children, fontClass }: { children: ReactNode; fontClass: string }) {
  const pathname = usePathname() ?? "/academy";
  const route = routeOf(pathname);
  return (
    <div className={`iipa ${fontClass}${route === "signin" ? " signin" : ""}`} data-route={route} id="iipa">
      <AcademyProvider>
        <Frame route={route}>{children}</Frame>
      </AcademyProvider>
    </div>
  );
}

function Frame({ route, children }: { route: string; children: ReactNode }) {
  const { me, learner, closeModal, zenithOpen, setZenithOpen, signIn } = useAcademy();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  // The Academy is a normal scrolling page. The portal's global styles make the body the scroller for IIP's own screens.
  useEffect(() => {
    document.documentElement.classList.add("iipa-page");
    document.body.classList.add("iipa-page");
    return () => { document.documentElement.classList.remove("iipa-page"); document.body.classList.remove("iipa-page"); };
  }, []);

  useEffect(() => { setMenuOpen(false); }, [pathname]);
  useEffect(() => {
    const away = (event: MouseEvent) => { if (menuRef.current && !menuRef.current.contains(event.target as Node)) setMenuOpen(false); };
    const keys = (event: KeyboardEvent) => { if (event.key === "Escape") { setMenuOpen(false); setZenithOpen(false); closeModal(); } };
    document.addEventListener("click", away);
    document.addEventListener("keydown", keys);
    return () => { document.removeEventListener("click", away); document.removeEventListener("keydown", keys); };
  }, [closeModal, setZenithOpen]);

  // The Academy's floating assistant is off on the full Zenith page, and Zenith is hidden while a person is being assessed.
  useEffect(() => {
    document.getElementById("iipa")?.classList.toggle("aiw-open", zenithOpen && route !== "assistant");
  }, [zenithOpen, route]);

  const key = navKey(route);
  const unread = learner?.notifications.some((entry) => !entry.read) ?? false;
  const signedIn = me?.signedIn === true;
  const backHref = signedIn && me.hasOrganisation ? "/app" : "/";


  return (
    <>
      <header className="top">
        <div className="top-in">
          <Link className="logo" href="/academy" aria-label="IIP Academy home">
            <img src="/academy/iip-logo.svg" alt="" style={{ height: 44, width: "auto" }} />
            <div><b>IIP Academy</b><small>Skills for the future of insurance</small></div>
          </Link>
          <nav className="tnav" id="tnav" aria-label="Academy">
            {NAV.map(([id, icon, label, href]) => (
              <Link key={id} href={href} className={key === id ? "on" : ""}>
                <Icon name={icon} size={19} />
                {label}
              </Link>
            ))}
          </nav>
          <div className="tright">
            <a className="backiip" href={backHref} data-testid="back-to-iip">
              <Icon name="chevL" size={16} />
              <span>Back to IIP</span>
            </a>
            {signedIn ? (
              <>
                <Link className="bell" href="/academy/notifications" aria-label="Notifications">
                  <Icon name="bell" size={22} />
                  <span className="dot" style={{ display: unread ? "block" : "none" }} />
                </Link>
                <div ref={menuRef} className={`user${menuOpen ? " open" : ""}`} onClick={(event) => { if (!(event.target as HTMLElement).closest(".menu a, .menu button")) setMenuOpen((open) => !open); }}>
                  <Avatar name={learner?.profile.name || me.name} size={42} />
                  <span className="uname">{learner?.profile.name || me.name}</span>
                  <span className="chv"><Icon name="chevD" size={16} /></span>
                  <div className="menu" id="menuBox">
                    <Link href="/academy/profile"><Icon name="user" size={18} />My profile</Link>
                    <Link href="/academy/progress"><Icon name="chart" size={18} />My progress</Link>
                    <Link href="/academy/certificates"><Icon name="award" size={18} />Certificates</Link>
                    <Link href="/academy/settings"><Icon name="settings" size={18} />Settings</Link>
                    {me.isOperator ? <Link href="/academy/admin" data-testid="admin-link"><Icon name="layers" size={18} />Manage the Academy</Link> : null}
                    <a href={backHref}><Icon name="home" size={18} />Back to IIP</a>
                    <hr />
                    <button type="button" className="menuitem" onClick={() => void signOut()}><Icon name="logout" size={18} />Sign out</button>
                  </div>
                </div>
              </>
            ) : me ? (
              <button type="button" className="btn primary sm" onClick={signIn} data-testid="sign-in">Sign in</button>
            ) : null}
          </div>
        </div>
      </header>

      <main id="view">{children}</main>

      <nav className="bnav" id="bnav" aria-label="Academy">
        {NAV.map(([id, icon, label, href]) => (
          <Link key={id} href={href} className={key === id ? "on" : ""}>
            <Icon name={icon} size={22} />
            {label}
          </Link>
        ))}
      </nav>

      <ZenithWidget route={route} />
    </>
  );
}
