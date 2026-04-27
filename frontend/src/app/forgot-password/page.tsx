'use client';

import { useState } from 'react';
import Link from 'next/link';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [resetLink, setResetLink] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setResetLink('');
    setIsLoading(true);
    try {
      const res = await fetch(`${API_URL}/api/auth/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      });
      const data = await res.json();
      if (data.resetLink) {
        setResetLink(data.resetLink);
      } else {
        setError(data.message || 'No account found with that email.');
      }
    } catch {
      setError('Network error — try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900 to-slate-900 flex items-center justify-center p-4">
      <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 w-full max-w-md border border-white/20 shadow-2xl">
        <h1 className="text-2xl font-bold text-white mb-2">Reset Password</h1>
        <p className="text-gray-400 text-sm mb-6">Enter your email to get a reset link.</p>

        {!resetLink ? (
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="bg-red-500/20 border border-red-500/50 text-red-200 px-4 py-2 rounded-lg text-sm">{error}</div>
            )}
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="your@email.com"
              required
              className="w-full px-4 py-2 bg-white/10 border border-white/20 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-purple-500"
            />
            <button
              type="submit"
              disabled={isLoading}
              className="w-full bg-purple-600 text-white font-semibold py-3 rounded-lg hover:bg-purple-700 transition-colors disabled:opacity-50"
            >
              {isLoading ? 'Generating...' : 'Get Reset Link'}
            </button>
          </form>
        ) : (
          <div className="space-y-4">
            <div className="bg-green-500/20 border border-green-500/50 text-green-200 px-4 py-3 rounded-lg text-sm">
              Reset link generated. Click it to set a new password:
            </div>
            <a
              href={resetLink}
              className="block w-full text-center bg-purple-600 text-white font-semibold py-3 rounded-lg hover:bg-purple-700 transition-colors"
            >
              Reset my password
            </a>
          </div>
        )}

        <div className="mt-6 text-center text-sm">
          <Link href="/login" className="text-purple-400 hover:text-purple-300">&larr; Back to login</Link>
        </div>
      </div>
    </div>
  );
}
