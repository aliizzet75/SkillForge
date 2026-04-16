'use client';

import { useState, useEffect } from 'react';
import { useGameStore, initializeSignalR, enterLobby, playRandom, gameComplete, leaveLobby } from '@/store/gameStore';

export default function Home() {
  const [playerName, setPlayerName] = useState('');
  const [avatar, setAvatar] = useState('🧙‍♀️');
  const [selectedColors, setSelectedColors] = useState<string[]>([]);
  const [roundStartTime, setRoundStartTime] = useState<number>(0);
  const [showColors, setShowColors] = useState(false);
  
  const {
    user,
    isConnected,
    isInLobby,
    isMatchmaking,
    isInGame,
    currentGame,
    onlinePlayers,
    setUser,
    setInLobby,
    setMatchmaking,
    setInGame,
  } = useGameStore();

  // Initialize SignalR on mount
  useEffect(() => {
    initializeSignalR();
  }, []);

  const handleEnterLobby = async () => {
    if (!playerName.trim()) return;
    await enterLobby(playerName, avatar);
    setUser({ id: 'temp', username: playerName, skills: [] });
  };

  const handlePlayRandom = async () => {
    setMatchmaking(true);
    await playRandom();
  };

  const handleColorSelect = (color: string) => {
    if (!currentGame?.data) return;
    
    const newSelected = [...selectedColors, color];
    setSelectedColors(newSelected);
    
    if (newSelected.length === currentGame.data.length) {
      const time = Date.now() - roundStartTime;
      gameComplete(100, time);
      setSelectedColors([]);
    }
  };

  const handleStartGame = () => {
    setShowColors(false);
    setRoundStartTime(Date.now());
    setSelectedColors([]);
  };

  // Lobby Screen
  if (isInLobby && !isInGame) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
        <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
          <h1 className="text-3xl font-bold text-white text-center mb-6">🧠 SkillForge Lobby</h1>
          
          <div className="mb-6">
            <p className="text-white/80 mb-2">Willkommen, <span className="font-bold text-white">{playerName}</span></p>
            <p className="text-white/60 text-sm flex items-center gap-2">
              <span className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500' : 'bg-red-500'}`}></span>
              {isConnected ? 'Verbunden' : 'Verbindung wird hergestellt...'}
            </p>
          </div>

          {isMatchmaking ? (
            <div className="text-center py-8">
              <div className="animate-spin text-4xl mb-4">⏳</div>
              <p className="text-white">Suche nach Gegner...</p>
            </div>
          ) : (
            <div className="space-y-4">
              <button
                onClick={handlePlayRandom}
                className="w-full py-4 px-6 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 text-white font-bold rounded-xl transition-all transform hover:scale-[1.02]"
              >
                🎲 Zufälliger Gegner
              </button>
              
              <div className="border-t border-white/20 pt-4 mt-4">
                <p className="text-white/60 text-sm mb-3">Online Spieler: {onlinePlayers.length}</p>
                <div className="flex gap-2 overflow-x-auto pb-2">
                  {onlinePlayers.map((player) => (
                    <div key={player.id} className="flex-shrink-0 bg-white/10 rounded-lg px-3 py-2 text-sm text-white">
                      {player.username}
                    </div>
                  ))}
                </div>
              </div>

              <button
                onClick={leaveLobby}
                className="w-full py-3 px-6 bg-white/10 hover:bg-white/20 text-white rounded-xl transition-all"
              >
                Verlassen
              </button>
            </div>
          )}
        </div>
      </div>
    );
  }

  // Game Screen (Memory Colors)
  if (isInGame && currentGame) {
    const { data, round, totalRounds, opponentName } = currentGame;

    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
        <div className="max-w-2xl w-full">
          {/* Header */}
          <div className="flex justify-between items-center mb-6 text-white">
            <div>
              <span className="text-sm text-white/60">Runde {round}/{totalRounds}</span>
              <h2 className="text-xl font-bold">Gegner: {opponentName}</h2>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-2xl">{avatar}</span>
              <span className="text-white/60">vs</span>
              <span className="text-2xl">{currentGame.opponentAvatar || '🧙‍♂️'}</span>
            </div>
          </div>

          {/* Game Area */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
            {/* Show Colors Phase */}
            {showColors ? (
              <div className="text-center">
                <p className="text-white mb-4">Merke dir die Reihenfolge:</p>
                <div className="flex justify-center gap-4 text-6xl mb-6">
                  {data?.map((color: string, i: number) => (
                    <div key={i} className="animate-pulse">{color}</div>
                  ))}
                </div>
                <button
                  onClick={handleStartGame}
                  className="py-3 px-8 bg-green-600 hover:bg-green-700 text-white font-bold rounded-xl"
                >
                  ✅ Fertig
                </button>
              </div>
            ) : (
              <div>
                <p className="text-white text-center mb-4">Klicke die Farben in der richtigen Reihenfolge:</p>
                
                {/* Selected Colors */}
                <div className="flex justify-center gap-2 mb-6 min-h-[3rem]">
                  {selectedColors.map((color, i) => (
                    <span key={i} className="text-4xl">{color}</span>
                  ))}
                </div>

                {/* Color Grid */}
                <div className="grid grid-cols-4 gap-4">
                  {['🔴', '🟢', '🔵', '🟡', '🟣', '🟠', '⚫', '⚪'].map((color) => {
                    const isSelected = selectedColors.includes(color) && 
                      data?.indexOf(color) === selectedColors.indexOf(color);
                    const isWrong = selectedColors.includes(color) &&
                      data?.indexOf(color) !== selectedColors.indexOf(color);
                    
                    return (
                      <button
                        key={color}
                        onClick={() => handleColorSelect(color)}
                        disabled={selectedColors.includes(color)}
                        className={`
                          p-4 text-4xl rounded-xl transition-all transform hover:scale-110
                          ${isWrong ? 'bg-red-500/50 opacity-50' : ''}
                          ${isSelected ? 'bg-green-500/50' : 'bg-white/10 hover:bg-white/20'}
                          ${selectedColors.includes(color) ? 'opacity-50' : ''}
                        `}
                      >
                        {color}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }

  // Login Screen
  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
      <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
        <h1 className="text-4xl font-bold text-white text-center mb-2">🧠 SkillForge</h1>
        <p className="text-white/60 text-center mb-8">Trainiere dein Gehirn. Messe deine Skills.</p>

        <div className="space-y-4">
          <div>
            <label className="block text-white/80 mb-2 text-sm">Dein Name</label>
            <input
              type="text"
              value={playerName}
              onChange={(e) => setPlayerName(e.target.value)}
              placeholder="Max Mustermann"
              className="w-full px-4 py-3 bg-white/10 border border-white/20 rounded-xl text-white placeholder:text-white/40 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div>
            <label className="block text-white/80 mb-2 text-sm">Wähle deinen Avatar</label>
            <div className="grid grid-cols-5 gap-2">
              {['🧙‍♀️', '🧙‍♂️', '🦸‍♀️', '🦸‍♂️', '👩‍🔬', '👨‍🔬', '🧚‍♀️', '🧚‍♂️', '👩‍🚀', '👨‍🚀'].map((a) => (
                <button
                  key={a}
                  onClick={() => setAvatar(a)}
                  className={`
                    p-2 text-2xl rounded-lg transition-all
                    ${avatar === a ? 'bg-indigo-600' : 'bg-white/10 hover:bg-white/20'}
                  `}
                >
                  {a}
                </button>
              ))}
            </div>
          </div>

          <button
            onClick={handleEnterLobby}
            disabled={!playerName.trim() || !isConnected}
            className="w-full py-4 px-6 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-bold rounded-xl transition-all transform hover:scale-[1.02] mt-6"
          >
            {isConnected ? 'Lobby betreten →' : 'Verbinde...'}
          </button>
        </div>
      </div>
    </div>
  );
}
