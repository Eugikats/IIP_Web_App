import type { Metadata } from "next";
import { IBM_Plex_Mono, Inter, Plus_Jakarta_Sans, Source_Serif_4 } from "next/font/google";
import { Toaster } from "sonner";
import LovableVisualParityGate from "@/components/iip/LovableVisualParityGate";
import { ThemeProvider } from "@/components/ThemeProvider";
import "./globals.css";
import "./lovable.css";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

// The sign-in page uses CIP's face, so the two products read as one family.
const plusJakartaSans = Plus_Jakarta_Sans({
  subsets: ["latin"],
  variable: "--font-login",
  display: "swap",
  weight: ["400", "500", "600", "700"],
});

const sourceSerif = Source_Serif_4({
  subsets: ["latin"],
  variable: "--font-cc-serif",
  display: "swap",
});

const ibmPlexMono = IBM_Plex_Mono({
  subsets: ["latin"],
  weight: ["400", "500", "600"],
  variable: "--font-ibm-plex-mono",
  display: "swap",
});

export const metadata: Metadata = {
  title: "IIP Operations",
  description: "AI-powered claims operations command center for modern insurers. Human-supervised, auditable, and Azure-native."
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning className={`${inter.variable} ${plusJakartaSans.variable} ${ibmPlexMono.variable} ${sourceSerif.variable}`}>
      <body className="font-sans antialiased" suppressHydrationWarning>
        <ThemeProvider>
          <LovableVisualParityGate>{children}</LovableVisualParityGate>
          <Toaster position="top-right" richColors closeButton />
        </ThemeProvider>
      </body>
    </html>
  );
}
