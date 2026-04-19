'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authService, User } from '@/services/auth';

export default function LobbyPage() {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const currentUser = authService.getUser();
    if (!currentUser) {
      router.push('/login');
      return;
    }
    setUser(currentUser);
    setIsLoading(false);
  }, [router]);

  const handleLogout = () => {
    authService.logout();
    router.push('/login');
  };

  if (isLoading) {
    return <div className="min-h-screen bg-slate-900 flex items-center justify-center">
      <div className="text-white">Loading...</div>
    </div>;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900 to-slate-900 p-8">
      <div className="max-w-6xl mx-auto">
        {/* Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-white mb-1">SkillForge Lobby</h1>
            <p className="text-gray-400">Welcome back, {user?.displayName || user?.username}! 🎮</p>
          </div>
          
          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-purple-400 font-bold">Level {user?.currentLevel}</div>
              <div className="text-sm text-gray-400">{user?.totalXp} XP</div>
            </div>
            <button
              onClick={handleLogout}
              className="bg-red-600 text-white px-4 py-2 rounded-lg hover:bg-red-700 transition-colors"
            >
              Logout
            </button>
          </div>
        </div>

        {/* Game Modes */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🎲 Play Random</h2>
            <p className="text-gray-300 mb-4">Match with a random opponent and test your skills!</p>
            <button className="w-full bg-purple-600 text-white py-3 rounded-lg font-semibold hover:bg-purple-700 transition-colors">
              Find Match
            </button>
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🏆 Ranked</h2>
            <p className="text-gray-300 mb-4">Compete in ranked matches and climb the leaderboard!</p>
            <button className="w-full bg-blue-600 text-white py-3 rounded-lg font-semibold hover:bg-blue-700 transition-colors">
              Play Ranked
            </button>
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">👤 Solo Training</h2>
            <p className="text-gray-300 mb-4">Practice against AI and improve your skills!</p>
            <button className="w-full bg-green-600 text-white py-3 rounded-lg font-semibold hover:bg-green-700 transition-colors">
              Start Training
            </button>
          </div>
        </div>

        {/* Leaderboard Preview */}
        <div className="mt-8 bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
          <h2 className="text-2xl font-bold text-white mb-4">🏆 Global Leaderboard</h2>
          <div className="space-y-2">
            {[1, 2, 3, 4, 5].map((rank) => (
              <div key={rank} className="flex items-center gap-4 p-3 bg-white/5 rounded-lg">
                <div className="w-8 text-center font-bold text-purple-400">#{rank}</div>
                <div className="w-10 h-10 bg-purple-600 rounded-full"></div>
                <div className="flex-1 text-white">Player {rank}</div>
                <div className="text-purple-400 font-bold">{10000 - rank * 500} XP</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
