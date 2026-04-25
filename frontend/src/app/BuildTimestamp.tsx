export default function BuildTimestamp() {
  const buildTime = process.env.NEXT_PUBLIC_BUILD_TIME;
  if (!buildTime) return null;

  return (
    <p className="text-white/40 text-center text-xs mb-2">
      Build: {new Date(buildTime).toLocaleString("de-DE")}
    </p>
  );
}
