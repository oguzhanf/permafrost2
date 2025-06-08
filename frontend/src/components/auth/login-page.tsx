'use client';

import { useMsal } from '@azure/msal-react';
import { loginRequest } from '@/lib/auth';
import { useState } from 'react';
import { LoadingSpinner } from '@/components/ui/loading-spinner';

export function LoginPage() {
  const { instance } = useMsal();
  const [isLoading, setIsLoading] = useState(false);

  const handleLogin = async () => {
    setIsLoading(true);
    try {
      await instance.loginRedirect(loginRequest);
    } catch (error) {
      console.error('Login failed:', error);
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div>
          <div className="mx-auto h-12 w-12 flex items-center justify-center rounded-full bg-primary-100">
            <svg
              className="h-8 w-8 text-primary-600"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
              />
            </svg>
          </div>
          <h2 className="mt-6 text-center text-3xl font-bold tracking-tight text-gray-900">
            Permafrost Identity Management
          </h2>
          <p className="mt-2 text-center text-sm text-gray-600">
            Sign in with your Azure Active Directory account
          </p>
        </div>

        <div className="mt-8 space-y-6">
          <div className="rounded-md bg-blue-50 p-4">
            <div className="flex">
              <div className="flex-shrink-0">
                <svg
                  className="h-5 w-5 text-blue-400"
                  fill="currentColor"
                  viewBox="0 0 20 20"
                >
                  <path
                    fillRule="evenodd"
                    d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                    clipRule="evenodd"
                  />
                </svg>
              </div>
              <div className="ml-3">
                <h3 className="text-sm font-medium text-blue-800">
                  Secure Access Required
                </h3>
                <div className="mt-2 text-sm text-blue-700">
                  <p>
                    This application requires authentication through Azure Active Directory.
                    You will be redirected to Microsoft's secure login page.
                  </p>
                </div>
              </div>
            </div>
          </div>

          <div>
            <button
              onClick={handleLogin}
              disabled={isLoading}
              className="group relative w-full flex justify-center py-3 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isLoading ? (
                <>
                  <LoadingSpinner size="sm" className="mr-2" />
                  Signing in...
                </>
              ) : (
                <>
                  <svg
                    className="w-5 h-5 mr-2"
                    viewBox="0 0 24 24"
                    fill="currentColor"
                  >
                    <path d="M23.64 12.204c0-.815-.073-1.598-.21-2.35H12.18v4.442h6.458c-.278 1.494-1.125 2.76-2.398 3.61v2.995h3.878c2.269-2.09 3.578-5.17 3.578-8.697z" />
                    <path d="M12.18 24c3.24 0 5.956-1.075 7.942-2.91l-3.878-2.995c-1.075.72-2.45 1.145-4.064 1.145-3.125 0-5.77-2.11-6.715-4.945H1.505v3.09C3.48 21.295 7.545 24 12.18 24z" />
                    <path d="M5.465 14.295c-.24-.72-.375-1.49-.375-2.295s.135-1.575.375-2.295V6.615H1.505C.55 8.52 0 10.695 0 13s.55 4.48 1.505 6.385l3.96-3.09z" />
                    <path d="M12.18 4.755c1.76 0 3.34.605 4.585 1.79l3.435-3.435C18.135 1.19 15.42 0 12.18 0 7.545 0 3.48 2.705 1.505 6.615l3.96 3.09c.945-2.835 3.59-4.95 6.715-4.95z" />
                  </svg>
                  Sign in with Microsoft
                </>
              )}
            </button>
          </div>

          <div className="text-center">
            <p className="text-xs text-gray-500">
              By signing in, you agree to our terms of service and privacy policy.
              This application is for authorized users only.
            </p>
          </div>
        </div>

        <div className="mt-8 border-t border-gray-200 pt-6">
          <div className="text-center text-xs text-gray-500">
            <p>Permafrost Identity Management System v1.0.0</p>
            <p className="mt-1">
              Secure • Compliant • Monitored
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
