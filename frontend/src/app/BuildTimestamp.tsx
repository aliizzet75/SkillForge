"use client";
import { useState, useEffect } from "react";

export default function BuildTimestamp() {
  const [buildTime, setBuildTime] = useState<string | null>(null);

  useEffect(() => {
    fetch("/build-info.json")
      .then((r) => r.json())
      .then((d) => d.buildTime && setBuildTime(d.buildTime))
      .catch(() => {});
  }, []);

  if (!buildTime) return null;

  return (
    <p className="text-white/40 text-center text-xs mb-2">
      Build: {new Date(buildTime).toLocaleString("de-DE")}
    </p>
  );
}
