'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001';

type SkillType = 'overall' | 'memory';

interface LeaderboardEntry {
  rank: number;
  userId: string;
  username: string;
  countryCode: string | null;
  level: number;
  xp: number;
  percentile: number;
  gamesPlayed: number;
  gamesWon: number;
}

interface LeaderboardResponse {
  skillType: string;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  users: LeaderboardEntry[];
}

const FLAG_MAP: Record<string, string> = {
  DE: '🇩🇪', TR: '🇹🇷', US: '🇺🇸', GB: '🇬🇧', FR: '🇫🇷',
  ES: '🇪🇸', IT: '🇮🇹', PL: '🇵🇱', RU: '🇷🇺', BR: '🇧🇷',
};

function flag(code: string | null) {
  if (!code) return '';
  return FLAG_MAP[code.toUpperCase()] ?? code.toUpperCase();
}

function winRate(played: number, won: number) {
  if (played === 0) return '—';
  return `${Math.round((won / played) * 100)}%`;
}

export default function LeaderboardPage() {
  const router = useRouter();
  const [skillType, setSkillType] = useState<SkillType>('overall');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<LeaderboardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLeaderboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(
        `${API_URL}/api/leaderboard/global?skillType=${skillType}&page=${page}&pageSize=20`
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setData(await res.json());
    } catch (e: any) {
      setError(e.message ?? 'Fehler beim Laden');
    } finally {
      setLoading(false);
    }
  }, [skillType, page]);

  useEffect(() => {
    fetchLeaderboard();
  }, [fetchLeaderboard]);

  // Reset to page 1 when switching skill type
  const handleSkillTypeChange = (type: SkillType) => {
    setSkillType(type);
    setPage(1);
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 p-4">
      <div className="max-w-3xl mx-auto">

        {/* Header */}
        <div className="flex items-center gap-4 mb-8">
          <button
            onClick={() => router.push('/')}
            className="text-white/60 hover:text-white transition-colors text-sm"
          >
            ← Zurück
          </button>
          <h1 className="text-3xl font-bold text-white">🏆 Leaderboard</h1>
        </div>

        {/* Skill Type Tabs */}
        <div className="flex gap-2 mb-6">
          {(['overall', 'memory'] as SkillType[]).map((type) => (
            <button
              key={type}
              onClick={() => handleSkillTypeChange(type)}
              className={`px-5 py-2 rounded-xl font-semibold text-sm transition-all ${
                skillType === type
                  ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-500/30'
                  : 'bg-white/10 text-white/60 hover:bg-white/20 hover:text-white'
              }`}
            >
              {type === 'overall' ? '🌐 Gesamt' : '🧠 Memory'}
            </button>
          ))}
          <span className="ml-auto text-white/40 text-sm self-center">
            {data ? `${data.totalCount} Spieler` : ''}
          </span>
        </div>

        {/* Table */}
        <div className="bg-white/5 backdrop-blur-lg rounded-2xl border border-white/10 overflow-hidden">
          {/* Table Header */}
          <div className="grid grid-cols-[3rem_1fr_5rem_5rem_5rem_4rem] gap-2 px-4 py-3 border-b border-white/10 text-white/50 text-xs font-semibold uppercase tracking-wider">
            <span className="text-center">#</span>
            <span>Spieler</span>
            <span className="text-right">Level</span>
            <span className="text-right">XP</span>
            <span className="text-right">Percentile</span>
            <span className="text-right">Win%</span>
          </div>

          {loading && (
            <div className="py-16 text-center text-white/40">
              <div className="animate-spin text-3xl mb-3">⏳</div>
              Lade...
            </div>
          )}

          {error && (
            <div className="py-16 text-center text-red-400">
              ⚠️ {error}
            </div>
          )}

          {!loading && !error && data?.users.length === 0 && (
            <div className="py-16 text-center text-white/40">
              Noch keine Einträge vorhanden
            </div>
          )}

          {!loading && !error && data?.users.map((entry, i) => {
            const isTop3 = entry.rank <= 3;
            const medal = entry.rank === 1 ? '🥇' : entry.rank === 2 ? '🥈' : entry.rank === 3 ? '🥉' : null;

            return (
              <div
                key={entry.userId}
                className={`grid grid-cols-[3rem_1fr_5rem_5rem_5rem_4rem] gap-2 px-4 py-3 border-b border-white/5 transition-colors hover:bg-white/5 ${
                  isTop3 ? 'bg-white/[0.03]' : ''
                }`}
              >
                {/* Rank */}
                <span className="text-center font-bold text-white/70 self-center">
                  {medal ?? <span className="text-white/40">{entry.rank}</span>}
                </span>

                {/* Player */}
                <div className="flex items-center gap-2 min-w-0">
                  <span className="text-sm">{flag(entry.countryCode)}</span>
                  <span className={`font-semibold truncate ${isTop3 ? 'text-white' : 'text-white/80'}`}>
                    {entry.username}
                  </span>
                </div>

                {/* Level */}
                <div className="text-right self-center">
                  <span className="text-indigo-400 font-semibold text-sm">Lvl {entry.level}</span>
                </div>

                {/* XP */}
                <div className="text-right self-center">
                  <span className="text-white/70 text-sm">{entry.xp.toLocaleString('de-DE')}</span>
                </div>

                {/* Percentile */}
                <div className="text-right self-center">
                  <span className={`text-sm font-semibold ${
                    entry.percentile >= 90 ? 'text-yellow-400' :
                    entry.percentile >= 70 ? 'text-green-400' :
                    'text-white/60'
                  }`}>
                    {entry.percentile.toFixed(1)}%
                  </span>
                </div>

                {/* Win Rate */}
                <div className="text-right self-center">
                  <span className="text-white/50 text-sm">{winRate(entry.gamesPlayed, entry.gamesWon)}</span>
                </div>
              </div>
            );
          })}
        </div>

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-center gap-3 mt-6">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-4 py-2 rounded-xl bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 disabled:cursor-not-allowed transition-all text-sm"
            >
              ← Zurück
            </button>
            <span className="text-white/60 text-sm">
              Seite {page} / {data.totalPages}
            </span>
            <button
              onClick={() => setPage(p => Math.min(data.totalPages, p + 1))}
              disabled={page === data.totalPages}
              className="px-4 py-2 rounded-xl bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 disabled:cursor-not-allowed transition-all text-sm"
            >
              Weiter →
            </button>
          </div>
        )}

      </div>
    </div>
  );
}
