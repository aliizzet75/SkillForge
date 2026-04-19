const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001';

export interface User {
  id: number;
  username: string;
  email?: string;
  displayName?: string;
  avatar?: string;
  totalXp: number;
  currentLevel: number;
}

export interface AuthResponse {
  token: string;
  user: User;
}

class AuthService {
  private token: string | null = null;
  private user: User | null = null;

  constructor() {
    // Load from localStorage on client side
    if (typeof window !== 'undefined') {
      this.token = localStorage.getItem('token');
      const userStr = localStorage.getItem('user');
      if (userStr) {
        this.user = JSON.parse(userStr);
      }
    }
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }

  getUser(): User | null {
    return this.user;
  }

  getToken(): string | null {
    return this.token;
  }

  logout(): void {
    this.token = null;
    this.user = null;
    if (typeof window !== 'undefined') {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    }
  }

  setAuth(token: string, user: User): void {
    this.token = token;
    this.user = user;
    if (typeof window !== 'undefined') {
      localStorage.setItem('token', token);
      localStorage.setItem('user', JSON.stringify(user));
    }
  }

  async fetchWithAuth(url: string, options: RequestInit = {}): Promise<Response> {
    const headers = {
      'Content-Type': 'application/json',
      ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
      ...options.headers,
    };

    return fetch(`${API_URL}${url}`, {
      ...options,
      headers,
    });
  }

  // Handle OAuth callback
  async handleOAuthCallback(provider: string, code: string): Promise<AuthResponse> {
    const response = await fetch(`${API_URL}/api/auth/${provider}/callback`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code }),
    });

    if (!response.ok) {
      throw new Error('OAuth authentication failed');
    }

    const data: AuthResponse = await response.json();
    this.setAuth(data.token, data.user);
    return data;
  }
}

export const authService = new AuthService();
