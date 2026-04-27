'use client';

import { useState, useEffect } from 'react';
import { useGameStore, initializeSignalR, enterLobby, playRandom, cancelMatchmaking, challengePlayer, acceptChallenge, declineChallenge, submitAnswer, leaveLobby } from '@/store/gameStore';
import BuildTimestamp from './BuildTimestamp';

export default function Home() {
  const [playerName, setPlayerName] = useState('');
  const [avatar, setAvatar] = useState('🧙‍♀️');
  const [selectedColors, setSelectedColors] = useState<string[]>([]);
  const [challengeSent, setChallengeSent] = useState<string | null>(null);

  const {
    user,
    isConnected,
    isInLobby,
    isMatchmaking,
    isInGame,
    currentGame,
    onlinePlayers,
    showColors,
    isInputPhase,
    roundResults,
    matchResults,
    opponentDisconnected,
    incomingChallenge,
    setUser,
    setInLobby,
    setMatchmaking,
    setOpponentDisconnected,
    setIncomingChallenge,
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

  const handleCancelMatchmaking = async () => {
    await cancelMatchmaking();
  };

  const handleChallengePlayer = async (targetName: string) => {
    setChallengeSent(targetName);
    await challengePlayer(targetName);
  };

  const handleAcceptChallenge = async () => {
    await acceptChallenge();
    setMatchmaking(true);
  };

  const handleDeclineChallenge = async () => {
    await declineChallenge();
  };

  const handleColorSelect = (color: string) => {
    if (!isInputPhase || !currentGame?.data) return;

    const newSelected = [...selectedColors, color];
    setSelectedColors(newSelected);

    if (newSelected.length === currentGame.data.length) {
      submitAnswer(newSelected);
      setSelectedColors([]);
    }
  };

  const handleStartInputPhase = () => {
    setSelectedColors([]);
  };

  const handlePlayAgain = () => {
    useGameStore.setState({
      isInGame: false,
      currentGame: null,
      matchResults: null,
      roundResults: null,
      showColors: false,
      isInputPhase: false,
    });
    setSelectedColors([]);
  };

  // Opponent Disconnected Screen
  if (opponentDisconnected) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
        <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20 text-center">
          <h1 className="text-3xl font-bold text-white mb-6">⚠️ Spiel unterbrochen</h1>

          <div className="text-6xl mb-4">🔌</div>

          <p className="text-xl font-bold text-white mb-6">
            Dein Gegner hat das Spiel verlassen
          </p>

          <button
            onClick={() => {
              leaveLobby();
              setOpponentDisconnected(false);
            }}
            className="w-full py-4 px-6 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 text-white font-bold rounded-xl transition-all"
          >
            Zurück zur Lobby
          </button>
        </div>
      </div>
    );
  }

  // Lobby Screen
  if (isInLobby && !isInGame) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
        <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
          <h1 className="text-3xl font-bold text-white text-center mb-6">🧠 SkillForge Lobby</h1>

          {/* Incoming Challenge Modal */}
          {incomingChallenge && (
            <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
              <div className="bg-slate-800 border border-white/20 rounded-2xl p-8 max-w-sm w-full text-center">
                <div className="text-5xl mb-3">{incomingChallenge.fromAvatar}</div>
                <h2 className="text-xl font-bold text-white mb-2">Herausforderung!</h2>
                <p className="text-white/70 mb-6">
                  <span className="font-bold text-white">{incomingChallenge.fromPlayerName}</span> fordert dich heraus
                </p>
                <div className="flex gap-3">
                  <button
                    onClick={handleAcceptChallenge}
                    className="flex-1 py-3 bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white font-bold rounded-xl transition-all"
                  >
                    ✅ Annehmen
                  </button>
                  <button
                    onClick={handleDeclineChallenge}
                    className="flex-1 py-3 bg-white/10 hover:bg-white/20 text-white rounded-xl transition-all"
                  >
                    ❌ Ablehnen
                  </button>
                </div>
              </div>
            </div>
          )}

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
              <p className="text-white mb-4">
                {challengeSent ? `Warte auf Antwort von ${challengeSent}...` : 'Suche nach Gegner...'}
              </p>
              <button
                onClick={handleCancelMatchmaking}
                className="py-2 px-6 bg-white/10 hover:bg-white/20 text-white rounded-xl transition-all text-sm"
              >
                Abbrechen
              </button>
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
                <div className="flex flex-col gap-2">
                  {onlinePlayers
                    .filter(p => p.username !== playerName)
                    .map((player) => (
                      <div key={player.id} className="flex items-center justify-between bg-white/10 rounded-lg px-3 py-2">
                        <div className="flex items-center gap-2">
                          {player.avatar && <span className="text-xl">{player.avatar}</span>}
                          <span className="text-sm text-white">{player.username}</span>
                        </div>
                        <button
                          onClick={() => handleChallengePlayer(player.username)}
                          className="text-xs py-1 px-3 bg-indigo-500/30 hover:bg-indigo-500/60 text-indigo-300 hover:text-white rounded-lg transition-all"
                        >
                          ⚔️ Herausfordern
                        </button>
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

  // Match Over Screen
  if (matchResults && isInGame) {
    const { Results, Player1Score, Player2Score, Player1XpEarned, Player2XpEarned, Winner, IsTie } = matchResults;
    const myPlayerLabel = useGameStore.getState().myPlayerLabel || '';
    const isWinner = Winner === myPlayerLabel;
    const myXp = myPlayerLabel === 'Player1' ? Player1XpEarned : Player2XpEarned;
    const isLoggedIn = typeof window !== 'undefined' && !!localStorage.getItem('token');

    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-900 via-indigo-900 to-slate-900 flex flex-col items-center justify-center p-4">
        <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20 text-center">
          <h1 className="text-3xl font-bold text-white mb-6">🏁 Spiel beendet!</h1>

          {IsTie ? (
            <div className="text-6xl mb-4">🤝</div>
          ) : isWinner ? (
            <div className="text-6xl mb-4">🏆</div>
          ) : (
            <div className="text-6xl mb-4">😔</div>
          )}

          <p className="text-2xl font-bold text-white mb-2">
            {IsTie ? 'Unentschieden!' : isWinner ? 'Du hast gewonnen!' : 'Du hast verloren!'}
          </p>

          {myXp && <p className="text-green-400 text-xl font-bold mb-1">+{myXp} XP</p>}
          {!isLoggedIn && (
            <p className="text-white/50 text-sm mb-3">
              <a href="/login" className="text-indigo-400 underline">Einloggen</a>, um XP dauerhaft zu speichern
            </p>
          )}

          <div className="flex justify-center gap-8 my-6">
            <div className="text-center">
              <p className="text-white/60 text-sm">Spieler 1</p>
              <p className="text-3xl font-bold text-white">{Player1Score || 0}</p>
              {Player1XpEarned && <p className="text-green-400 text-xs mt-1">+{Player1XpEarned} XP</p>}
            </div>
            <div className="text-center">
              <p className="text-white/60 text-sm">Spieler 2</p>
              <p className="text-3xl font-bold text-white">{Player2Score || 0}</p>
              {Player2XpEarned && <p className="text-green-400 text-xs mt-1">+{Player2XpEarned} XP</p>}
            </div>
          </div>

          <button
            onClick={handlePlayAgain}
            className="w-full py-4 px-6 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 text-white font-bold rounded-xl transition-all"
          >
            🔄 Nochmal spielen
          </button>
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

          {/* Round Results Panel */}
          {roundResults && (
            <div className="bg-white/20 backdrop-blur-lg rounded-xl p-6 mb-6 border border-white/30">
              <h3 className="text-xl font-bold text-white text-center mb-4">Runde {roundResults.Round} Ergebnis</h3>
              <div className="grid grid-cols-2 gap-4">
                {roundResults.Results && Object.entries(roundResults.Results).map(([playerId, result]: [string, any]) => (
                  <div key={playerId} className="bg-white/10 rounded-lg p-4 text-center">
                    <p className="text-white/60 text-sm">{result.PlayerAlias || 'Spieler'}</p>
                    <p className="text-2xl font-bold text-white">{result.TotalScore} Punkte</p>
                    <p className="text-green-400 text-sm">+{result.Score} diese Runde</p>
                    {result.IsPerfect && <span className="text-yellow-400 text-xs">⭐ Perfekt!</span>}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Game Area */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
            {/* Show Colors Phase */}
            {showColors ? (
              <div className="text-center">
                <p className="text-white mb-4 text-lg">👀 Merke dir die Reihenfolge:</p>
                <div className="flex justify-center gap-4 text-7xl mb-6">
                  {data?.map((color: string, i: number) => (
                    <div key={i} className="animate-pulse">{color}</div>
                  ))}
                </div>
                <p className="text-white/60 text-sm">Die Farben verschwinden automatisch...</p>
              </div>
            ) : isInputPhase ? (
              <div>
                <p className="text-white text-center mb-4 text-lg">🖱️ Klicke die Farben in der richtigen Reihenfolge:</p>

                {/* Selected Colors */}
                <div className="flex justify-center gap-2 mb-6 min-h-[3rem]">
                  {selectedColors.map((color, i) => (
                    <span key={i} className="text-4xl">{color}</span>
                  ))}
                  {/* Placeholder for remaining colors */}
                  {data && Array.from({ length: data.length - selectedColors.length }).map((_, i) => (
                    <span key={`placeholder-${i}`} className="text-4xl text-white/20">⭕</span>
                  ))}
                </div>

                {/* Color Grid */}
                <div className="grid grid-cols-4 gap-4">
                  {['🔴', '🟢', '🔵', '🟡', '🟣', '🟠', '⚫', '⚪'].map((color) => {
                    const isSelected = selectedColors.includes(color);

                    return (
                      <button
                        key={color}
                        onClick={() => handleColorSelect(color)}
                        disabled={isSelected}
                        className={`
                          p-4 text-4xl rounded-xl transition-all transform hover:scale-110
                          ${isSelected ? 'bg-gray-500/50 opacity-30' : 'bg-white/10 hover:bg-white/30'}
                        `}
                      >
                        {color}
                      </button>
                    );
                  })}
                </div>
              </div>
            ) : (
              <div className="text-center py-8">
                <div className="animate-spin text-4xl mb-4">⏳</div>
                <p className="text-white">Warte auf nächste Runde...</p>
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
        <BuildTimestamp />

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

          <div className="border-t border-white/10 pt-4 flex gap-3">
            <a href="/login" className="flex-1 py-2 text-center text-sm text-white/60 hover:text-white bg-white/5 hover:bg-white/10 rounded-xl transition-all">
              🔑 Einloggen
            </a>
            <a href="/register" className="flex-1 py-2 text-center text-sm text-white/60 hover:text-white bg-white/5 hover:bg-white/10 rounded-xl transition-all">
              📝 Registrieren
            </a>
            <a href="/leaderboard" className="flex-1 py-2 text-center text-sm text-white/60 hover:text-white bg-white/5 hover:bg-white/10 rounded-xl transition-all">
              🏆 Leaderboard
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}
