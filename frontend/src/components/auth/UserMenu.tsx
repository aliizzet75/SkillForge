'use client';

import { useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import Link from 'next/link';

export default function UserMenu() {
  const { user, logout, isAuthenticated } = useAuth();
  const [isOpen, setIsOpen] = useState(false);

  if (!isAuthenticated || !user) {
    return (
      <Link
        href="/login"
        className="bg-purple-600 text-white px-4 py-2 rounded-lg hover:bg-purple-700 transition-colors font-medium"
      >
        Log In
      </Link>
    );
  }

  return (
    <div className="relative">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 bg-white/10 hover:bg-white/20 px-4 py-2 rounded-lg transition-colors"
      >
        <div className="w-8 h-8 flex items-center justify-center text-2xl">
          {user.avatar || user.username.charAt(0).toUpperCase()}
        </div>
        <div className="text-left hidden sm:block">
          <p className="text-white font-medium text-sm">{user.username}</p>
          <p className="text-gray-400 text-xs">Level {user.currentLevel} • {user.totalXp} XP</p>
        </div>
        <svg
          className={`w-4 h-4 text-gray-400 transition-transform ${isOpen ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {isOpen && (
        <>
          <div
            className="fixed inset-0 z-40"
            onClick={() => setIsOpen(false)}
          />
          <div className="absolute right-0 mt-2 w-48 bg-white/10 backdrop-blur-lg rounded-lg border border-white/20 shadow-xl z-50">
            <div className="p-3 border-b border-white/20 sm:hidden">
              <p className="text-white font-medium">{user.username}</p>
              <p className="text-gray-400 text-sm">Level {user.currentLevel} • {user.totalXp} XP</p>
            </div>
            <Link
              href="/profile"
              className="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white transition-colors"
              onClick={() => setIsOpen(false)}
            >
              👤 Profil
            </Link>
            <Link
              href="/settings"
              className="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white transition-colors"
              onClick={() => setIsOpen(false)}
            >
              ⚙️ Einstellungen
            </Link>
            <button
              onClick={() => {
                logout();
                setIsOpen(false);
              }}
              className="w-full text-left px-4 py-2 text-red-400 hover:bg-white/10 transition-colors rounded-b-lg"
            >
              Log Out
            </button>
          </div>
        </>
      )}
    </div>
  );
}
