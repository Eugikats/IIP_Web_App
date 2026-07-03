import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
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
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5167/api/:path*",
      },
    ];
  },
};

export default nextConfig;
