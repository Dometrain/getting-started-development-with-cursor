const storageKey = 'livepoll-token';

function readStoredToken(): string | null {
  try {
    return sessionStorage.getItem(storageKey);
  } catch {
    return null;
  }
}

function writeStoredToken(value: string | null): void {
  try {
    if (value === null) {
      sessionStorage.removeItem(storageKey);
      return;
    }

    sessionStorage.setItem(storageKey, value);
  } catch {
    // The page can still vote during this visit.
  }
}

let token: string | null = readStoredToken();

export function getToken(): string | null {
  return token;
}

export function setToken(value: string): void {
  token = value;
  writeStoredToken(value);
}

export function clearToken(): void {
  token = null;
  writeStoredToken(null);
}

export function authorizationHeader(): Record<string, string> {
  if (token === null) {
    return {};
  }

  return { Authorization: `Bearer ${token}` };
}

type JwtPayload = Record<string, unknown>;

function decodeJwtPayload(jwt: string): JwtPayload | null {
  const parts = jwt.split('.');
  const encodedPayload = parts[1];
  if (parts.length < 2 || encodedPayload === undefined) {
    return null;
  }

  try {
    const base64 = encodedPayload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = atob(padded);
    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}

function roleClaimIsAdmin(value: unknown): boolean {
  if (value === 'admin') {
    return true;
  }

  if (Array.isArray(value)) {
    return value.includes('admin');
  }

  return false;
}

/** True when the current (or given) JWT includes role=admin. */
export function tokenHasAdminRole(jwt: string | null = getToken()): boolean {
  if (jwt === null) {
    return false;
  }

  const payload = decodeJwtPayload(jwt);
  if (payload === null) {
    return false;
  }

  return roleClaimIsAdmin(payload.role);
}
