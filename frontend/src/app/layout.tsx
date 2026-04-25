import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { AuthProvider } from "@/contexts/AuthContext";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
});

export const metadata: Metadata = {
  title: "SkillForge - Train Your Brain",
  description: "Train your brain, track your skills, compete with others. Memory games, speed challenges, and skill tracking.",
  keywords: ["brain training", "memory games", "skill tracking", "competition"],
  manifest: "/manifest.json",
  appleWebApp: {
    capable: true,
    statusBarStyle: "black-translucent",
    title: "SkillForge",
  },
  icons: {
    icon: [
      { url: "/icon-192x192.png", sizes: "192x192", type: "image/png" },
      { url: "/icon-512x512.png", sizes: "512x512", type: "image/png" },
    ],
    apple: [
      { url: "/icon-192x192.png", sizes: "192x192", type: "image/png" },
    ],
  },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  themeColor: "#6366f1",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const buildTime = process.env.NEXT_PUBLIC_BUILD_TIME;
  const buildDisplay = buildTime ? new Date(buildTime).toLocaleString('de-DE') : '';

  return (
    <html lang="de" className={`${inter.variable} h-full antialiased`}>
      <head>
        <link rel="manifest" href="/manifest.json" />
        <link rel="apple-touch-icon" href="/icon-192x192.png" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
        <meta name="apple-mobile-web-app-title" content="SkillForge" />
        {buildDisplay && <meta name="build-time" content={buildDisplay} />}
      </head>
      <body className="min-h-full flex flex-col bg-slate-900">
        <AuthProvider>{children}</AuthProvider>
        {buildDisplay && (
          <div className="fixed bottom-2 right-2 text-xs text-white/30 bg-black/50 px-2 py-1 rounded">
            Build: {buildDisplay}
          </div>
        )}
      </body>
    </html>
  );
}
