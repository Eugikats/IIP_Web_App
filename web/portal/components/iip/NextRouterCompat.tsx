"use client";

import NextLink from "next/link";
import { useParams as useNextParams, usePathname, useRouter } from "next/navigation";
import { useEffect, type AnchorHTMLAttributes, type CSSProperties, type ReactNode } from "react";

type To = string;

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href"> & {
  to: To;
  replace?: boolean;
  children?: ReactNode;
};

export function Link({ to, children, ...props }: LinkProps) {
  return (
    <NextLink href={to} {...props}>
      {children}
    </NextLink>
  );
}

type NavLinkClassName =
  | string
  | ((state: { isActive: boolean; isPending: boolean }) => string | undefined);

type NavLinkProps = Omit<LinkProps, "className"> & {
  end?: boolean;
  className?: NavLinkClassName;
  style?: CSSProperties | ((state: { isActive: boolean; isPending: boolean }) => CSSProperties | undefined);
};

export function NavLink({ to, end, className, style, children, ...props }: NavLinkProps) {
  const pathname = usePathname();
  const isActive = end ? pathname === to : pathname === to || pathname.startsWith(`${to}/`);
  const state = { isActive, isPending: false };
  const computedClassName = typeof className === "function" ? className(state) : className;
  const computedStyle = typeof style === "function" ? style(state) : style;

  return (
    <NextLink href={to} className={computedClassName} style={computedStyle} {...props}>
      {children}
    </NextLink>
  );
}

export function useNavigate() {
  const router = useRouter();
  return (to: string | number, options?: { replace?: boolean }) => {
    if (typeof to === "number") {
      if (to < 0) router.back();
      return;
    }

    if (options?.replace) router.replace(to);
    else router.push(to);
  };
}

export function useLocation() {
  const pathname = usePathname();
  return { pathname };
}

export function useParams<T extends Record<string, string | string[] | undefined> = Record<string, string>>() {
  return useNextParams() as T;
}

export function Navigate({ to, replace }: { to: string; replace?: boolean }) {
  const router = useRouter();

  useEffect(() => {
    if (replace) router.replace(to);
    else router.push(to);
  }, [replace, router, to]);

  return null;
}

export function Outlet() {
  return null;
}
