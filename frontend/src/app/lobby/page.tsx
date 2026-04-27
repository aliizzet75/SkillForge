'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AuthProvider, useAuth } from '@/contexts/AuthContext';
import UserMenu from '@/components/auth/UserMenu';
import { 
  enterLobby, 
  leaveLobby, 
  playRandom, 
  cancelMatchmaking,
  useGameStore,
  disconnectSignalR
} from '@/store/gameStore';

function LobbyPageContent() {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuth();
  const [selectedGameType, setSelectedGameType] = useState(1); // 1 = Memory, 2 = Speed
  const [isConnecting, setIsConnecting] = useState(false);
  
  const { 
    isConnected, 
    isInLobby, 
    isMatchmaking,
    onlinePlayers 
  } = useGameStore();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [isLoading, isAuthenticated, router]);

  // Connect to SignalR when user is authenticated
  useEffect(() => {
    if (user && isAuthenticated && !isInLobby && !isConnecting) {
      setIsConnecting(true);
      enterLobby(user.username, user.avatar || '🧙‍♀️')
        .then(() => setIsConnecting(false))
        .catch((err) => {
          console.error('Failed to enter lobby:', err);
          setIsConnecting(false);
        });
    }

    // Cleanup on unmount
    return () => {
      if (isInLobby) {
        leaveLobby();
        disconnectSignalR();
      }
    };
  }, [user, isAuthenticated, isInLobby, isConnecting]);

  const handleFindMatch = async () => {
    if (!isConnected) return;
    await playRandom(selectedGameType);
  };

  const handleCancelMatchmaking = async () => {
    await cancelMatchmaking();
  };

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
        {/* Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-white mb-1">SkillForge Lobby</h1>
            <p className="text-gray-400">
              Welcome back, {user?.username || 'Player'}! 🎮
            </p>
            {isConnected && (
              <span className="text-green-400 text-sm">● Connected</span>
            )}
          </div>
          <UserMenu />
        </div>

        {/* Game Type Selection */}
        <div className="mb-6">
          <h3 className="text-white font-semibold mb-3">Select Game Mode:</h3>
          <div className="flex gap-4">
            <button
              onClick={() => setSelectedGameType(1)}
              className={`px-6 py-3 rounded-lg font-semibold transition-colors ${
                selectedGameType === 1 
                  ? 'bg-purple-600 text-white' 
                  : 'bg-white/10 text-gray-300 hover:bg-white/20'
              }`}
            >
              🧠 Memory
            </button>
            <button
              onClick={() => setSelectedGameType(2)}
              className={`px-6 py-3 rounded-lg font-semibold transition-colors ${
                selectedGameType === 2 
                  ? 'bg-yellow-600 text-white' 
                  : 'bg-white/10 text-gray-300 hover:bg-white/20'
              }`}
            >
              ⚡ Reaktion
            </button>
          </div>
        </div>

        {/* Game Actions */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🎲 Play Random</h2>
            <p className="text-gray-300 mb-4">
              Match with a random opponent and test your skills!
            </p>
            {isMatchmaking ? (
              <button
                onClick={handleCancelMatchmaking}
                className="w-full bg-red-600 text-white py-3 rounded-lg font-semibold hover:bg-red-700 transition-colors"
              >
                Cancel Matchmaking...
              </button>
            ) : (
              <button
                onClick={handleFindMatch}
                disabled={!isConnected}
                className="w-full bg-purple-600 text-white py-3 rounded-lg font-semibold hover:bg-purple-700 transition-colors disabled:opacity-50"
              >
                Find Match
              </button>
            )}
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">🏆 Ranked</h2>
            <p className="text-gray-300 mb-4">
              Compete in ranked matches and climb the leaderboard!
            </p>
            <button
              disabled
              className="w-full bg-blue-600 text-white py-3 rounded-lg font-semibold opacity-50 cursor-not-allowed"
            >
              Coming Soon
            </button>
          </div>

          <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20">
            <h2 className="text-xl font-bold text-white mb-4">👤 Solo Training</h2>
            <p className="text-gray-300 mb-4">
              Practice against AI and improve your skills!
            </p>
            <button
              disabled
              className="w-full bg-green-600 text-white py-3 rounded-lg font-semibold opacity-50 cursor-not-allowed"
            >
              Coming Soon
            </button>
          </div>
        </div>

        {/* Online Players */}
        <div className="bg-white/10 backdrop-blur rounded-xl p-6 border border-white/20 mb-8">
          <h2 className="text-xl font-bold text-white mb-4">
            👥 Online Players ({onlinePlayers.length})
          </h2>
          {onlinePlayers.length === 0 ? (
            <p className="text-gray-400">No other players online</p>
          ) : (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {onlinePlayers.map((player) => (
                <div 
                  key={player.id}
                  className="bg-white/5 rounded-lg p-3 flex items-center gap-3"
                >
                  <span className="text-2xl">{player.avatar || '🧠'}</span>
                  <div>
                    <p className="text-white font-medium">{player.username}</p>
                    <p className="text-gray-400 text-sm">{player.countryCode || '🌍'}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
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
