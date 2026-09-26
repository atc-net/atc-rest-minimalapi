import { afterEach, describe, expect, it, vi } from 'vitest';
import { createUser, deleteUser, listUsers, updateUser, type User } from './usersApi';

const user: User = {
  id: '00000000-0000-0000-0000-000000000007',
  gender: 'Male',
  firstName: 'Ada',
  lastName: 'Tester',
  email: 'ada@example.com',
  telephone: null,
  homePage: null,
  homeAddress: null,
  workAddress: { streetName: null, streetNumber: null, postalCode: null, cityName: 'Aarhus', country: null },
};

function mockFetch(status: number, body?: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
    }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => vi.unstubAllGlobals());

describe('usersApi', () => {
  it('lists users', async () => {
    const fetchMock = mockFetch(200, [user]);

    await expect(listUsers()).resolves.toEqual([user]);
    expect(fetchMock).toHaveBeenCalledWith('/api/users', expect.anything());
  });

  it('creates a user', async () => {
    const fetchMock = mockFetch(201);

    const result = await createUser({ firstName: 'Ada' });

    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith('/api/users', expect.objectContaining({ method: 'POST', body: '{"firstName":"Ada"}' }));
  });

  it('returns the field errors of a ValidationProblem (400)', async () => {
    mockFetch(400, { title: 'One or more validation errors occurred.', status: 400, errors: { firstName: ['Too short.'] } });

    const result = await createUser({ firstName: 'A' });

    expect(result).toEqual({ ok: false, status: 400, errors: { firstName: ['Too short.'] }, message: 'One or more validation errors occurred.' });
  });

  it('returns the "detail" of a ProblemDetails error (e.g. a 409 conflict)', async () => {
    mockFetch(409, {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
      title: 'Conflict',
      status: 409,
      detail: "A user already exists with the email 'ada@example.com'.",
    });

    const result = await createUser({ email: 'ada@example.com' });

    expect(result).toEqual({ ok: false, status: 409, errors: {}, message: "A user already exists with the email 'ada@example.com'." });
  });

  it('returns a message for other errors (e.g. a 409 with a plain text body)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('"A user already exists"', { status: 409 })));

    const result = await createUser({ email: 'ada@example.com' });

    expect(result).toEqual({ ok: false, status: 409, errors: {}, message: '"A user already exists"' });
  });

  it('updates a user and returns it', async () => {
    const fetchMock = mockFetch(200, user);

    const result = await updateUser(user.id, { lastName: 'Tester' });

    expect(result).toEqual({ ok: true, user });
    expect(fetchMock).toHaveBeenCalledWith(`/api/users/${user.id}`, expect.objectContaining({ method: 'PUT' }));
  });

  it('deletes a user', async () => {
    const fetchMock = mockFetch(204);

    await expect(deleteUser(user.id)).resolves.toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith(`/api/users/${user.id}`, expect.objectContaining({ method: 'DELETE' }));
  });
});
