'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AuthProvider, useAuth } from '@/contexts/AuthContext';
import UserMenu from '@/components/auth/UserMenu';

function LobbyPageContent() {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [isLoading, isAuthenticated, router]);

  if (isLoading || !isAuthenticated) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-white">Loading...</div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900 to-slate-900 p-8">
      <div className="max-w-6xl mx-auto">
        {/* Header mit UserMenu */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-white mb-1">SkillForge Lobby</h1>
            <p className="text-gray-400">
              Welcome back, {user?.username || 'Player'}! 🎮
            </p>
          </div>
          <UserMenu />
        </div>

        {/* Game Modes */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🎲 Play Random</h2>
            <p className="text-gray-300 mb-4">
              Match with a random opponent and test your skills!
            </p>
            <button className="w-full bg-purple-600 text-white py-3 rounded-lg font-semibold hover:bg-purple-700 transition-colors">
              Find Match
            </button>
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🏆 Ranked</h2>
            <p className="text-gray-300 mb-4">
              Compete in ranked matches and climb the leaderboard!
            </p>
            <button className="w-full bg-blue-600 text-white py-3 rounded-lg font-semibold hover:bg-blue-700 transition-colors">
              Play Ranked
            </button>
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">👤 Solo Training</h2>
            <p className="text-gray-300 mb-4">
              Practice against AI and improve your skills!
            </p>
            <button className="w-full bg-green-600 text-white py-3 rounded-lg font-semibold hover:bg-green-700 transition-colors">
              Start Training
            </button>
          </div>
        </div>

        {/* Leaderboard Link */}
        <div className="mt-8">
          <a
            href="/leaderboard"
            className="flex items-center justify-between bg-white/10 backdrop-blur rounded-xl p-5 border border-white/20 hover:bg-white/15 transition-all group"
          >
            <div>
              <h2 className="text-xl font-bold text-white">🏆 Global Leaderboard</h2>
              <p className="text-gray-400 text-sm mt-1">Sieh wer die besten Memory-Spieler sind</p>
            </div>
            <span className="text-white/40 group-hover:text-white/80 transition-colors text-xl">→</span>
          </a>
        </div>
      </div>
    </div>
  );
}

export default function LobbyPage() {
  return (
    <AuthProvider>
      <LobbyPageContent />
    </AuthProvider>
  );
}
