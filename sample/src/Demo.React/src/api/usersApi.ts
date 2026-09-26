import type { FieldErrors, ValidationProblem } from '../validationProblem';

export type GenderType = 'None' | 'NonBinary' | 'Male' | 'Female';

export interface Country {
  name: string | null;
  alpha2Code: string | null;
  alpha3Code: string | null;
}

export interface Address {
  streetName: string | null;
  streetNumber: string | null;
  postalCode: string | null;
  cityName: string | null;
  country: Country | null;
}

export interface User {
  id: string;
  gender: GenderType;
  firstName: string;
  lastName: string;
  email: string;
  telephone: string | null;
  homePage: string | null;
  homeAddress: Address | null;
  workAddress: Address | null;
}

/** A JSON request body built from the form's wire paths (see forms/userFields.ts). */
export type RequestBody = Record<string, unknown>;

export type ApiFailure = {
  ok: false;
  status: number;
  /** Field errors keyed by the names the client sent (a 400 ValidationProblem), otherwise empty. */
  errors: FieldErrors;
  message: string;
};

export type ApiResult = { ok: true } | ApiFailure;

const jsonHeaders = { 'Content-Type': 'application/json', Accept: 'application/json' };

export async function listUsers(): Promise<User[]> {
  const response = await fetch('/api/users', { headers: { Accept: 'application/json' } });
  if (!response.ok) {
    throw new Error(`Loading users failed with status ${response.status}.`);
  }

  return (await response.json()) as User[];
}

export async function createUser(body: RequestBody): Promise<ApiResult> {
  const response = await fetch('/api/users', { method: 'POST', headers: jsonHeaders, body: JSON.stringify(body) });
  return response.ok ? { ok: true } : toFailure(response);
}

export async function updateUser(id: string, body: RequestBody): Promise<{ ok: true; user: User } | ApiFailure> {
  const response = await fetch(`/api/users/${id}`, { method: 'PUT', headers: jsonHeaders, body: JSON.stringify(body) });
  return response.ok ? { ok: true, user: (await response.json()) as User } : toFailure(response);
}

export async function deleteUser(id: string): Promise<ApiResult> {
  const response = await fetch(`/api/users/${id}`, { method: 'DELETE' });
  return response.ok ? { ok: true } : toFailure(response);
}

async function toFailure(response: Response): Promise<ApiFailure> {
  // Errors from the API are RFC 9457 ProblemDetails: field errors (a 400 ValidationProblem) or a "detail" message.
  if (response.headers.get('Content-Type')?.includes('application/problem+json')) {
    const problem = (await response.json()) as ValidationProblem & { detail?: string };
    return {
      ok: false,
      status: response.status,
      errors: problem.errors ?? {},
      message: problem.detail ?? problem.title ?? response.statusText,
    };
  }

  return { ok: false, status: response.status, errors: {}, message: (await response.text()) || response.statusText };
}
