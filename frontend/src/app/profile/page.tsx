'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001';

interface Skill {
  type: string;
  level: number;
  xp: number;
  percentile: number;
  gamesPlayed: number;
  gamesWon: number;
}

interface Insight {
  skillType: string;
  message: string;
  xpGained: number;
  percentileChange: number;
}

interface ProfileData {
  id: string;
  username: string;
  countryCode: string | null;
  createdAt: string;
  lastSeenAt: string | null;
  skills: Skill[];
}

interface InsightsData {
  insights: Insight[];
  streakDays: number;
}

// ─── SVG Radar Chart ─────────────────────────────────────────────────────────

function RadarChart({ skills }: { skills: Skill[] }) {
  const size = 220;
  const center = size / 2;
  const radius = 80;

  const axes = [
    { label: 'Memory', value: skills.find(s => s.type === 'memory')?.percentile ?? 0 },
    { label: 'Win Rate', value: skills.length > 0 ? (skills[0].gamesWon / Math.max(skills[0].gamesPlayed, 1)) * 100 : 0 },
    { label: 'Aktivität', value: Math.min(skills.reduce((a, s) => a + s.gamesPlayed, 0) / 2, 100) },
    { label: 'Gesamt', value: skills.find(s => s.type === 'overall')?.percentile ?? 0 },
    { label: 'Level', value: Math.min((skills.find(s => s.type === 'overall')?.level ?? 1) * 10, 100) },
  ];

  const n = axes.length;
  const angleStep = (2 * Math.PI) / n;
  const startAngle = -Math.PI / 2;

  function point(value: number, index: number): [number, number] {
    const angle = startAngle + index * angleStep;
    const r = (value / 100) * radius;
    return [center + r * Math.cos(angle), center + r * Math.sin(angle)];
  }

  function axisEnd(index: number): [number, number] {
    const angle = startAngle + index * angleStep;
    return [center + radius * Math.cos(angle), center + radius * Math.sin(angle)];
  }

  function labelPos(index: number): [number, number] {
    const angle = startAngle + index * angleStep;
    const r = radius + 20;
    return [center + r * Math.cos(angle), center + r * Math.sin(angle)];
  }

  const dataPoints = axes.map((a, i) => point(a.value, i));
  const dataPath = dataPoints.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ') + ' Z';

  // Grid rings at 25%, 50%, 75%, 100%
  const gridRings = [25, 50, 75, 100].map(pct => {
    const pts = axes.map((_, i) => point(pct, i));
    return pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'} ${x} ${y}`).join(' ') + ' Z';
  });

  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="mx-auto">
      {/* Grid rings */}
      {gridRings.map((d, i) => (
        <path key={i} d={d} fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="1" />
      ))}
      {/* Axis lines */}
      {axes.map((_, i) => {
        const [ex, ey] = axisEnd(i);
        return <line key={i} x1={center} y1={center} x2={ex} y2={ey} stroke="rgba(255,255,255,0.15)" strokeWidth="1" />;
      })}
      {/* Data polygon */}
      <path d={dataPath} fill="rgba(99,102,241,0.3)" stroke="rgb(129,140,248)" strokeWidth="2" />
      {/* Data points */}
      {dataPoints.map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r="4" fill="rgb(129,140,248)" />
      ))}
      {/* Labels */}
      {axes.map((a, i) => {
        const [lx, ly] = labelPos(i);
        return (
          <text key={i} x={lx} y={ly} textAnchor="middle" dominantBaseline="middle"
            fontSize="10" fill="rgba(255,255,255,0.7)" className="select-none">
            {a.label}
          </text>
        );
      })}
    </svg>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function ProfilePage() {
  const router = useRouter();
  const { user, token, isLoading, isAuthenticated } = useAuth();
  const [profile, setProfile] = useState<ProfileData | null>(null);
  const [insights, setInsights] = useState<InsightsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [isLoading, isAuthenticated, router]);

  useEffect(() => {
    if (!user?.id) return;

    const headers = token ? { Authorization: `Bearer ${token}` } : {};

    Promise.all([
      fetch(`${API_URL}/api/users/${user.id}`, { headers }),
      fetch(`${API_URL}/api/users/${user.id}/insights`, { headers }),
    ])
      .then(async ([pRes, iRes]) => {
        if (!pRes.ok) throw new Error('Profil nicht gefunden');
        const [p, ins] = await Promise.all([pRes.json(), iRes.ok ? iRes.json() : { insights: [], streakDays: 0 }]);
        setProfile(p);
        setInsights(ins);
      })
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [user?.id, token]);

  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex items-center justify-center">
        <div className="animate-spin text-4xl">⏳</div>
      </div>
    );
  }

  if (error || !profile) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex items-center justify-center">
        <p className="text-red-400">⚠️ {error ?? 'Profil nicht gefunden'}</p>
      </div>
    );
  }

  const overallSkill = profile.skills.find(s => s.type === 'overall');
  const memorySkill = profile.skills.find(s => s.type === 'memory');
  const totalGames = overallSkill?.gamesPlayed ?? 0;
  const totalWins = overallSkill?.gamesWon ?? 0;
  const winRate = totalGames > 0 ? Math.round((totalWins / totalGames) * 100) : 0;

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 p-4">
      <div className="max-w-2xl mx-auto">

        {/* Header */}
        <div className="flex items-center gap-4 mb-6">
          <button onClick={() => router.back()} className="text-white/60 hover:text-white transition-colors text-sm">
            ← Zurück
          </button>
          <h1 className="text-2xl font-bold text-white">Mein Profil</h1>
        </div>

        {/* Profile Card */}
        <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-6 border border-white/20 mb-4">
          <div className="flex items-center gap-4 mb-6">
            <div className="w-16 h-16 rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-2xl font-bold text-white">
              {profile.username[0].toUpperCase()}
            </div>
            <div>
              <h2 className="text-xl font-bold text-white">{profile.username}</h2>
              {profile.countryCode && <p className="text-white/60 text-sm">{profile.countryCode}</p>}
              {insights && insights.streakDays > 1 && (
                <p className="text-orange-400 text-sm font-semibold">🔥 {insights.streakDays} Tage Streak</p>
              )}
            </div>
            <div className="ml-auto text-right">
              <p className="text-2xl font-bold text-indigo-400">Lvl {overallSkill?.level ?? 1}</p>
              <p className="text-white/60 text-sm">{overallSkill?.xp?.toLocaleString('de-DE') ?? 0} XP</p>
            </div>
          </div>

          {/* Stats Row */}
          <div className="grid grid-cols-3 gap-3 mb-6">
            {[
              { label: 'Spiele', value: totalGames },
              { label: 'Siege', value: totalWins },
              { label: 'Win Rate', value: `${winRate}%` },
            ].map(stat => (
              <div key={stat.label} className="bg-white/5 rounded-xl p-3 text-center">
                <p className="text-xl font-bold text-white">{stat.value}</p>
                <p className="text-white/50 text-xs mt-1">{stat.label}</p>
              </div>
            ))}
          </div>

          {/* Radar Chart */}
          <div className="mb-4">
            <p className="text-white/60 text-sm text-center mb-3">Skill-Übersicht</p>
            <RadarChart skills={profile.skills} />
          </div>

          {/* Skill Bars */}
          {[overallSkill, memorySkill].filter(Boolean).map(skill => skill && (
            <div key={skill.type} className="mb-3">
              <div className="flex justify-between text-sm mb-1">
                <span className="text-white/70 capitalize">{skill.type === 'overall' ? 'Gesamt' : 'Memory'}</span>
                <span className="text-indigo-400 font-semibold">Level {skill.level} · Top {(100 - Number(skill.percentile)).toFixed(0)}%</span>
              </div>
              <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-indigo-500 to-purple-500 rounded-full transition-all"
                  style={{ width: `${skill.percentile}%` }}
                />
              </div>
            </div>
          ))}
        </div>

        {/* Insights */}
        {insights && insights.insights.length > 0 && (
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-5 border border-white/20 mb-4">
            <h3 className="text-lg font-bold text-white mb-3">💡 Wochenrückblick</h3>
            <div className="space-y-2">
              {insights.insights.map((ins, i) => (
                <div key={i} className="flex items-start gap-3 bg-white/5 rounded-xl p-3">
                  <span className="text-lg mt-0.5">{ins.xpGained > 0 ? '📈' : '📉'}</span>
                  <p className="text-white/80 text-sm">{ins.message}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Links */}
        <div className="flex gap-3">
          <a href="/leaderboard" className="flex-1 py-3 text-center text-sm bg-white/10 hover:bg-white/20 text-white/70 hover:text-white rounded-xl transition-all">
            🏆 Leaderboard
          </a>
          <a href="/" className="flex-1 py-3 text-center text-sm bg-indigo-600 hover:bg-indigo-700 text-white font-semibold rounded-xl transition-all">
            🎮 Spielen
          </a>
        </div>

      </div>
    </div>
  );
}
