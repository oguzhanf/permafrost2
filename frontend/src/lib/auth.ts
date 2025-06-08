import { Configuration, PublicClientApplication } from '@azure/msal-browser';

// MSAL configuration
const msalConfig: Configuration = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_AZURE_CLIENT_ID || '',
    authority: `https://login.microsoftonline.com/${process.env.NEXT_PUBLIC_AZURE_TENANT_ID || 'common'}`,
    redirectUri: process.env.NEXT_PUBLIC_AZURE_REDIRECT_URI || 'http://localhost:3000',
    postLogoutRedirectUri: process.env.NEXT_PUBLIC_AZURE_REDIRECT_URI || 'http://localhost:3000',
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) {
          return;
        }
        switch (level) {
          case 0: // Error
            console.error(message);
            return;
          case 1: // Warning
            console.warn(message);
            return;
          case 2: // Info
            console.info(message);
            return;
          case 3: // Verbose
            console.debug(message);
            return;
        }
      },
    },
  },
};

// Create MSAL instance
export const msalInstance = new PublicClientApplication(msalConfig);

// Login request configuration
export const loginRequest = {
  scopes: ['User.Read', 'Directory.Read.All', 'Group.Read.All'],
  prompt: 'select_account' as const,
};

// Silent request configuration
export const silentRequest = {
  scopes: ['User.Read', 'Directory.Read.All', 'Group.Read.All'],
  account: null as any,
};

// Graph API endpoints
export const graphConfig = {
  graphMeEndpoint: 'https://graph.microsoft.com/v1.0/me',
  graphUsersEndpoint: 'https://graph.microsoft.com/v1.0/users',
  graphGroupsEndpoint: 'https://graph.microsoft.com/v1.0/groups',
};

// Initialize MSAL
export const initializeMsal = async (): Promise<void> => {
  try {
    await msalInstance.initialize();
    
    // Handle redirect promise
    const response = await msalInstance.handleRedirectPromise();
    if (response) {
      console.log('Login successful:', response);
    }
  } catch (error) {
    console.error('MSAL initialization failed:', error);
    throw error;
  }
};

// Get access token
export const getAccessToken = async (): Promise<string | null> => {
  try {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length === 0) {
      return null;
    }

    const request = {
      ...silentRequest,
      account: accounts[0],
    };

    const response = await msalInstance.acquireTokenSilent(request);
    return response.accessToken;
  } catch (error) {
    console.error('Failed to acquire token silently:', error);
    
    try {
      // Fallback to interactive token acquisition
      const response = await msalInstance.acquireTokenPopup(loginRequest);
      return response.accessToken;
    } catch (interactiveError) {
      console.error('Failed to acquire token interactively:', interactiveError);
      return null;
    }
  }
};

// Check if user is authenticated
export const isAuthenticated = (): boolean => {
  const accounts = msalInstance.getAllAccounts();
  return accounts.length > 0;
};

// Get current user account
export const getCurrentAccount = () => {
  const accounts = msalInstance.getAllAccounts();
  return accounts.length > 0 ? accounts[0] : null;
};

// Login function
export const login = async (): Promise<void> => {
  try {
    await msalInstance.loginRedirect(loginRequest);
  } catch (error) {
    console.error('Login failed:', error);
    throw error;
  }
};

// Logout function
export const logout = async (): Promise<void> => {
  try {
    const account = getCurrentAccount();
    if (account) {
      await msalInstance.logoutRedirect({
        account,
        postLogoutRedirectUri: msalConfig.auth.postLogoutRedirectUri,
      });
    }
  } catch (error) {
    console.error('Logout failed:', error);
    throw error;
  }
};

// User roles and permissions
export const UserRoles = {
  ADMIN: 'Admin',
  USER: 'User',
  VIEWER: 'Viewer',
} as const;

export type UserRole = typeof UserRoles[keyof typeof UserRoles];

// Check user permissions
export const hasPermission = (requiredRole: UserRole): boolean => {
  const account = getCurrentAccount();
  if (!account) return false;

  // In a real implementation, you would check the user's roles from the token claims
  // For now, we'll assume all authenticated users have basic permissions
  const userRoles = account.idTokenClaims?.roles || [];
  
  switch (requiredRole) {
    case UserRoles.VIEWER:
      return true; // All authenticated users can view
    case UserRoles.USER:
      return userRoles.includes('User') || userRoles.includes('Admin');
    case UserRoles.ADMIN:
      return userRoles.includes('Admin');
    default:
      return false;
  }
};

// Get user display name
export const getUserDisplayName = (): string => {
  const account = getCurrentAccount();
  return account?.name || account?.username || 'Unknown User';
};

// Get user email
export const getUserEmail = (): string => {
  const account = getCurrentAccount();
  return account?.username || '';
};
