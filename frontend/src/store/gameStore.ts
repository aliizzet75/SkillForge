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
  
  // Actions
  setUser: (user: User | null) => void;
  setConnected: (connected: boolean) => void;
  setInLobby: (inLobby: boolean) => void;
  setMatchmaking: (matchmaking: boolean) => void;
  setInGame: (inGame: boolean) => void;
  setCurrentGame: (game: any) => void;
  setOnlinePlayers: (players: any[]) => void;
}

export const useGameStore = create<GameState>((set) => ({
  user: null,
  isConnected: false,
  isInLobby: false,
  isMatchmaking: false,
  isInGame: false,
  currentGame: null,
  onlinePlayers: [],
  
  setUser: (user) => set({ user }),
  setConnected: (isConnected) => set({ isConnected }),
  setInLobby: (isInLobby) => set({ isInLobby }),
  setMatchmaking: (isMatchmaking) => set({ isMatchmaking }),
  setInGame: (isInGame) => set({ isInGame }),
  setCurrentGame: (currentGame) => set({ currentGame }),
  setOnlinePlayers: (onlinePlayers) => set({ onlinePlayers }),
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
  
  connection.on('OpponentFinished', (playerName: string) => {
    console.log('Opponent finished:', playerName);
  });
  
  connection.on('RoundResult', (result: any) => {
    console.log('Round result:', result);
  });
  
  connection.on('MatchOver', (result: any) => {
    console.log('Match over:', result);
    useGameStore.getState().setInGame(false);
    useGameStore.getState().setCurrentGame(null);
  });
  
  connection.on('OpponentDisconnected', () => {
    console.log('Opponent disconnected');
  });
  
  connection.on('SoloModeActivated', () => {
    console.log('Solo mode activated');
  });
  
  connection.on('WaitingForOpponent', () => {
    useGameStore.getState().setMatchmaking(true);
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

export const gameComplete = async (score: number, timeMs: number) => {
  const conn = getSignalRConnection();
  if (conn) {
    await conn.invoke('GameComplete', score, timeMs);
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
