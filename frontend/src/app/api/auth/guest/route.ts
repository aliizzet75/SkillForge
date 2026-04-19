import { NextResponse } from 'next/server';

// Mock user storage (in-memory only)
const mockUsers: any[] = [];

export async function POST() {
  // Generate guest user
  const guestId = Math.random().toString(36).substring(2, 10);
  const user = {
    id: mockUsers.length + 1,
    username: `Guest_${guestId}`,
    displayName: `Guest ${guestId}`,
    email: null,
    avatar: null,
    totalXp: 0,
    currentLevel: 1,
    createdAt: new Date().toISOString(),
  };

  mockUsers.push(user);

  // Generate mock JWT token
  const token = `mock_jwt_${Buffer.from(JSON.stringify({ sub: user.id, username: user.username })).toString('base64')}`;

  return NextResponse.json({
    token,
    user,
  });
}
