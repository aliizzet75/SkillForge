import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  // Skip API routes and static files
  if (request.nextUrl.pathname.startsWith('/api/')) {
    return NextResponse.next();
  }

  const token = request.cookies.get('token')?.value;
  const { pathname } = request.nextUrl;

  // Protected routes
  const protectedRoutes = ['/lobby', '/game', '/profile'];
  const isProtected = protectedRoutes.some(route => pathname.startsWith(route));

  // Auth routes
  const authRoutes = ['/login'];
  const isAuthRoute = authRoutes.some(route => pathname.startsWith(route));

  // Redirect to login if accessing protected route without token
  if (isProtected && !token) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  // Redirect to lobby if accessing auth route with token
  if (isAuthRoute && token) {
    return NextResponse.redirect(new URL('/lobby', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)'],
};
