import type { NextConfig } from "next";
import { PHASE_DEVELOPMENT_SERVER } from "next/constants";

export default function portalConfig(phase: string): NextConfig {
  const configuredRuntimeProfile = process.env.NEXT_PUBLIC_RUNTIME_PROFILE?.trim();
  const configuredAuthMode = process.env.NEXT_PUBLIC_AUTH_MODE?.trim();

  if (process.env.IIP_PORTAL_BUILD === "1" && (!configuredRuntimeProfile || !configuredAuthMode)) {
    throw new Error("NEXT_PUBLIC_RUNTIME_PROFILE and NEXT_PUBLIC_AUTH_MODE are required for production portal builds.");
  }

  const runtimeProfile = (configuredRuntimeProfile || "Development").toLowerCase();
  const protectedRuntime = ["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile);
  const distDir = phase === PHASE_DEVELOPMENT_SERVER ? ".next-dev" : ".next";

  if (protectedRuntime && !process.env.NEXT_PUBLIC_API_BASE_URL?.trim()) {
    throw new Error("NEXT_PUBLIC_API_BASE_URL is required for protected portal builds.");
  }

  return {
    distDir,
    output: "standalone",
    // The dev tools badge overlaps the workspace profile menu on tablet-sized viewports.
    // Keep the browser surface unobstructed while retaining Next's full error overlay.
    devIndicators: false,
    // The portal uses plain img tags for local assets, not Next image optimization.
    // Exclude unused Sharp native packages from standalone tracing to avoid copying
    // every platform-specific libvips binary into Azure packaging artifacts.
    outputFileTracingExcludes: {
      "*": [
        "node_modules/@img/**/*",
        "node_modules/sharp/**/*",
        "node_modules/.pnpm/@img+*/**/*",
        "node_modules/.pnpm/sharp@*/**/*"
      ]
    },
    async rewrites() {
      if (protectedRuntime) return [];
      return [
        {
          source: "/api/:path*",
          destination: "http://localhost:5167/api/:path*",
        },
      ];
    },
    async redirects() {
      return [
        // The Academy is its own space now. Old links into the workspace version land on the new one.
        { source: "/app/academy", destination: "/academy", permanent: true },
        { source: "/app/academy/:courseId", destination: "/academy/courses/:courseId", permanent: true },
        {
          source: "/index",
          destination: "/",
          permanent: false,
        },
      ];
    },
  };
}
