'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';

const AVATARS = ['🧙‍♀️', '🧙‍♂️', '🦸‍♀️', '🦸‍♂️', '👩‍🔬', '👨‍🔬', '🧚‍♀️', '🧚‍♂️', '👩‍🚀', '👨‍🚀'];

function SettingsContent() {
  const { user, isAuthenticated, isLoading, updateAvatar, logout } = useAuth();
  const router = useRouter();
  const [selectedAvatar, setSelectedAvatar] = useState(user?.avatar ?? '🧙‍♀️');
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-white">Lade...</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    router.push('/login');
    return null;
  }

  const handleSave = async () => {
    setSaving(true);
    const ok = await updateAvatar(selectedAvatar);
    setSaving(false);
    if (ok) {
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900 to-slate-900 p-6 flex flex-col items-center justify-center">
      <div className="max-w-md w-full bg-white/10 backdrop-blur-lg rounded-2xl p-8 border border-white/20">
        <div className="flex items-center gap-3 mb-8">
          <a href="/" className="text-white/40 hover:text-white/80 transition-colors text-sm">←</a>
          <h1 className="text-2xl font-bold text-white">⚙️ Einstellungen</h1>
        </div>

        {/* Profile summary */}
        <div className="flex items-center gap-4 bg-white/5 rounded-xl p-4 mb-8">
          <span className="text-5xl">{selectedAvatar}</span>
          <div>
            <p className="text-white font-bold">{user?.username}</p>
            <p className="text-white/50 text-sm">Level {user?.currentLevel} · {user?.totalXp} XP</p>
          </div>
        </div>

        {/* Avatar picker */}
        <div className="mb-6">
          <p className="text-white/80 text-sm mb-3">Avatar wählen</p>
          <div className="grid grid-cols-5 gap-3">
            {AVATARS.map((a) => (
              <button
                key={a}
                onClick={() => setSelectedAvatar(a)}
                className={`p-3 text-3xl rounded-xl transition-all ${selectedAvatar === a ? 'bg-purple-600 ring-2 ring-purple-400 scale-110' : 'bg-white/10 hover:bg-white/20'}`}
              >
                {a}
              </button>
            ))}
          </div>
        </div>

        <button
          onClick={handleSave}
          disabled={saving || selectedAvatar === user?.avatar}
          className="w-full py-3 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 disabled:opacity-50 text-white font-bold rounded-xl transition-all mb-3"
        >
          {saving ? 'Speichern...' : saved ? '✅ Gespeichert!' : 'Speichern'}
        </button>

        <div className="border-t border-white/10 pt-4 flex gap-3">
          <a href="/profile" className="flex-1 py-2 text-center text-sm text-white/60 hover:text-white bg-white/5 hover:bg-white/10 rounded-xl transition-all">
            👤 Profil
          </a>
          <button
            onClick={() => { logout(); router.push('/login'); }}
            className="flex-1 py-2 text-center text-sm text-red-400 hover:text-red-300 bg-white/5 hover:bg-white/10 rounded-xl transition-all"
          >
            Abmelden
          </button>
        </div>
      </div>
    </div>
  );
}

export default function SettingsPage() {
  return <SettingsContent />;
}
