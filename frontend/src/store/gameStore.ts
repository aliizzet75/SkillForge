import { create } from 'zustand';
import * as signalR from '@microsoft/signalr';

interface User {
  id: string;
  username: string;
  countryCode?: string;
  skills: {
    type: string;
    level: number;
    xp: number;
    percentile: number;
  }[];
}

interface GameState {
  user: User | null;
  isConnected: boolean;
  isInLobby: boolean;
  isMatchmaking: boolean;
  isInGame: boolean;
  currentGame: {
    type: number;
    data: any;
    round: number;
    totalRounds: number;
    opponentName: string;
    opponentAvatar: string;
  } | null;
  onlinePlayers: { id: string; username: string; countryCode?: string }[];
  // Game phase
  showColors: boolean;
  isInputPhase: boolean;
  roundResults: any | null;
  matchResults: any | null;
  myPlayerLabel: string | null; // New field for player label
  opponentDisconnected: boolean; // New field for opponent disconnection
  
  // Actions
  setUser: (user: User | null) => void;
  setConnected: (connected: boolean) => void;
  setInLobby: (inLobby: boolean) => void;
  setMatchmaking: (matchmaking: boolean) => void;
  setInGame: (inGame: boolean) => void;
  setCurrentGame: (game: any) => void;
  setOnlinePlayers: (players: any[]) => void;
  setShowColors: (show: boolean) => void;
  setInputPhase: (isInput: boolean) => void;
  setRoundResults: (results: any) => void;
  setMatchResults: (results: any) => void;
  setMyPlayerLabel: (label: string | null) => void; // New action
  setOpponentDisconnected: (disconnected: boolean) => void; // New action
}

export const useGameStore = create<GameState>((set) => ({
  user: null,
  isConnected: false,
  isInLobby: false,
  isMatchmaking: false,
  isInGame: false,
  currentGame: null,
  onlinePlayers: [],
  showColors: false,
  isInputPhase: false,
  roundResults: null,
  matchResults: null,
  myPlayerLabel: null,
  opponentDisconnected: false,
  
  setUser: (user) => set({ user }),
  setConnected: (isConnected) => set({ isConnected }),
  setInLobby: (isInLobby) => set({ isInLobby }),
  setMatchmaking: (isMatchmaking) => set({ isMatchmaking }),
  setInGame: (isInGame) => set({ isInGame }),
  setCurrentGame: (currentGame) => set({ currentGame }),
  setOnlinePlayers: (onlinePlayers) => set({ onlinePlayers }),
  setShowColors: (showColors) => set({ showColors }),
  setInputPhase: (isInputPhase) => set({ isInputPhase }),
  setRoundResults: (roundResults) => set({ roundResults }),
  setMatchResults: (matchResults) => set({ matchResults }),
  setMyPlayerLabel: (myPlayerLabel) => set({ myPlayerLabel }),
  setOpponentDisconnected: (opponentDisconnected) => set({ opponentDisconnected }),
}));

// SignalR Connection
let connection: signalR.HubConnection | null = null;

export const getSignalRConnection = () => connection;

export const initializeSignalR = async () => {
  if (connection) return connection;
  
  const signalRUrl = process.env.NEXT_PUBLIC_SIGNALR_URL || 'http://localhost:5000/hubs/game';
  connection = new signalR.HubConnectionBuilder()
    .withUrl(signalRUrl)
    .withAutomaticReconnect()
    .build();
  
  // Event handlers
  connection.on('PlayerJoined', (playerName: string, avatar: string) => {
    console.log('Player joined:', playerName);
    // Update online players
  });
  
  connection.on('PlayerLeft', (playerName: string) => {
    console.log('Player left:', playerName);
  });
  
  connection.on('MatchFound', (opponentName: string, opponentAvatar: string, gameType: number, round: number, totalRounds: number) => {
    useGameStore.getState().setCurrentGame({
      type: gameType,
      data: null,
      round,
      totalRounds,
      opponentName,
      opponentAvatar,
    });
    useGameStore.getState().setInGame(true);
    useGameStore.getState().setMatchmaking(false);
  });
  
  connection.on('GameStarting', (gameType: number, gameData: any) => {
    const currentGame = useGameStore.getState().currentGame;
    if (currentGame) {
      useGameStore.getState().setCurrentGame({ ...currentGame, data: gameData });
    }
  });
  
  connection.on('ShowColors', (colors: any, durationMs: number) => {
    console.log('Show colors:', colors, 'for', durationMs, 'ms');
    const currentGame = useGameStore.getState().currentGame;
    if (currentGame) {
      useGameStore.getState().setCurrentGame({ ...currentGame, data: colors });
    }
    useGameStore.getState().setShowColors(true);
    useGameStore.getState().setInputPhase(false);
  });
  
  connection.on('HideColors', () => {
    console.log('Hide colors');
    useGameStore.getState().setShowColors(false);
  });
  
  connection.on('RoundInputPhase', () => {
    console.log('Round input phase started');
    useGameStore.getState().setInputPhase(true);
  });
  
  connection.on('RoundStarting', (round: number, totalRounds: number) => {
    console.log('Round starting:', round, 'of', totalRounds);
    useGameStore.getState().setRoundResults(null);
    useGameStore.getState().setShowColors(false);
    useGameStore.getState().setInputPhase(false);
  });
  
  connection.on('OpponentFinished', (playerName: string) => {
    console.log('Opponent finished:', playerName);
  });
  
  connection.on('RoundResult', (result: any) => {
    console.log('Round result:', result);
    useGameStore.getState().setRoundResults(result);
    useGameStore.getState().setShowColors(false);
    useGameStore.getState().setInputPhase(false);
  });
  
  connection.on('MatchOver', (result: any) => {
    console.log('Match over:', result);
    useGameStore.getState().setMatchResults(result);
    useGameStore.getState().setShowColors(false);
    useGameStore.getState().setInputPhase(false);
  });
  
  connection.on('OpponentDisconnected', () => {
    console.log('Opponent disconnected');
    useGameStore.getState().setOpponentDisconnected(true);
  });
  
  connection.on('SoloModeActivated', () => {
    console.log('Solo mode activated');
  });
  
  connection.on('WaitingForOpponent', () => {
    useGameStore.getState().setMatchmaking(true);
  });
  
  connection.on('PlayerAssigned', (label: string) => {
    console.log('Player assigned as:', label);
    useGameStore.getState().setMyPlayerLabel(label);
  });
  
  try {
    await connection.start();
    useGameStore.getState().setConnected(true);
    console.log('SignalR Connected');
  } catch (err) {
    console.error('SignalR Connection Error:', err);
    useGameStore.getState().setConnected(false);
  }
  
  return connection;
};

export const disconnectSignalR = async () => {
  if (connection) {
    await connection.stop();
    connection = null;
    useGameStore.getState().setConnected(false);
  }
};

// Game actions
export const enterLobby = async (playerName: string, avatar: string) => {
  const conn = await initializeSignalR();
  await conn.invoke('EnterLobby', playerName, avatar);
  useGameStore.getState().setInLobby(true);
};

export const playRandom = async () => {
  const conn = getSignalRConnection();
  if (conn) {
    await conn.invoke('PlayRandom');
    useGameStore.getState().setMatchmaking(true);
  }
};

export const submitAnswer = async (colors: string[]) => {
  const conn = getSignalRConnection();
  if (conn) {
    await conn.invoke('SubmitAnswer', colors);
  }
};

export const leaveLobby = async () => {
  const conn = getSignalRConnection();
  if (conn) {
    await conn.invoke('LeaveLobby');
    useGameStore.getState().setInLobby(false);
    useGameStore.getState().setMatchmaking(false);
    useGameStore.getState().setInGame(false);
    useGameStore.getState().setCurrentGame(null);
  }
};
