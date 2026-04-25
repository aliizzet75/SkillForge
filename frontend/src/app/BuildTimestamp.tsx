import { BUILD_TIME } from '@/lib/build-info';

export default function BuildTimestamp() {
  if (!BUILD_TIME || BUILD_TIME === 'unknown') return null;

  return (
    <p className="text-white/40 text-center text-xs mb-2">
      Build: {new Date(BUILD_TIME).toLocaleString('de-DE')}
    </p>
  );
}
