'use client';

import { useIsAuthenticated } from '@azure/msal-react';
import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { LoadingSpinner } from '@/components/ui/loading-spinner';
import { LoginPage } from '@/components/auth/login-page';
import { Dashboard } from '@/components/dashboard/dashboard';

export default function HomePage() {
  const isAuthenticated = useIsAuthenticated();
  const router = useRouter();

  useEffect(() => {
    if (isAuthenticated) {
      // User is authenticated, redirect to dashboard
      router.push('/dashboard');
    }
  }, [isAuthenticated, router]);

  if (isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="text-center">
          <LoadingSpinner size="lg" />
          <p className="mt-4 text-sm text-gray-600">Redirecting to dashboard...</p>
        </div>
      </div>
    );
  }

  return <LoginPage />;
}
