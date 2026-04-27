'use client';

import { useState, useEffect } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import Link from 'next/link';

const AVATARS = ['🧙‍♀️', '🧙‍♂️', '🦸‍♀️', '🦸‍♂️', '👩‍🔬', '👨‍🔬', '🧚‍♀️', '🧚‍♂️', '👩‍🚀', '👨‍🚀'];

export default function RegisterForm() {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [avatar, setAvatar] = useState('🧙‍♀️');
  const [error, setError] = useState('');
  const [registerAttempted, setRegisterAttempted] = useState(false);
  const { register, isLoading, isAuthenticated } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (registerAttempted && isAuthenticated) {
      router.push('/lobby');
    }
  }, [registerAttempted, isAuthenticated, router]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!username || !email || !password) {
      setError('Please fill in all fields');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    if (password.length < 6) {
      setError('Password must be at least 6 characters');
      return;
    }

    const result = await register(username, email, password, avatar);
    if (result === true) {
      setRegisterAttempted(true);
    } else {
      setError(result);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div className="bg-red-500/20 border border-red-500/50 text-red-200 px-4 py-2 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-gray-300 mb-2">
          Wähle deinen Avatar
        </label>
        <div className="grid grid-cols-5 gap-2">
          {AVATARS.map((a) => (
            <button
              key={a}
              type="button"
              onClick={() => setAvatar(a)}
              className={`p-2 text-2xl rounded-lg transition-all ${avatar === a ? 'bg-purple-600 ring-2 ring-purple-400' : 'bg-white/10 hover:bg-white/20'}`}
            >
              {a}
            </button>
          ))}
        </div>
      </div>

      <div>
        <label htmlFor="username" className="block text-sm font-medium text-gray-300 mb-1">
          Username
        </label>
        <input
          type="text"
          id="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="w-full px-4 py-2 bg-white/10 border border-white/20 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-purple-500"
          placeholder="Your username"
          required
        />
      </div>

      <div>
        <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-1">
          Email
        </label>
        <input
          type="email"
          id="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full px-4 py-2 bg-white/10 border border-white/20 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-purple-500"
          placeholder="you@example.com"
          required
        />
      </div>

      <div>
        <label htmlFor="password" className="block text-sm font-medium text-gray-300 mb-1">
          Password
        </label>
        <input
          type="password"
          id="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full px-4 py-2 bg-white/10 border border-white/20 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-purple-500"
          placeholder="••••••••"
          required
        />
      </div>

      <div>
        <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-300 mb-1">
          Confirm Password
        </label>
        <input
          type="password"
          id="confirmPassword"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          className="w-full px-4 py-2 bg-white/10 border border-white/20 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-purple-500"
          placeholder="••••••••"
          required
        />
      </div>

      <button
        type="submit"
        disabled={isLoading}
        className="w-full bg-purple-600 text-white font-semibold py-3 px-4 rounded-lg hover:bg-purple-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isLoading ? 'Creating account...' : 'Sign Up'}
      </button>

      <p className="text-center text-sm text-gray-400">
        Already have an account?{' '}
        <Link href="/login" className="text-purple-400 hover:text-purple-300">
          Log in
        </Link>
      </p>
    </form>
  );
}
