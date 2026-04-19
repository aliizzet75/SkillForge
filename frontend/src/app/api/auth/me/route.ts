import { NextResponse } from 'next/server';

// Parse mock JWT and return user info
export async function GET(request: Request) {
  const authHeader = request.headers.get('authorization');
  
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const token = authHeader.substring(7);
  
  try {
    // Parse mock token
    const base64Payload = token.replace('mock_jwt_', '');
    const payload = JSON.parse(Buffer.from(base64Payload, 'base64').toString());
    
    return NextResponse.json({
      id: payload.sub,
      username: payload.username,
      displayName: payload.username,
      email: null,
      avatar: null,
      totalXp: 0,
      currentLevel: 1,
    });
  } catch {
    return NextResponse.json({ error: 'Invalid token' }, { status: 401 });
  }
}
