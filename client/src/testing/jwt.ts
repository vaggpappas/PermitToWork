/**
 * Builds tokens shaped exactly like the ones the API issues, for tests.
 *
 * The signature is deliberate nonsense. That is not a shortcut — `decodeJwt` in
 * AuthService does not verify signatures and could not: a browser has no signing key, and
 * the token is only meaningful against an API that checks it properly. A fake signature is
 * therefore the honest shape of what the client is actually able to read.
 */
export function jwtWith(claims: Record<string, unknown>): string {
  const header = base64Url({ alg: 'HS256', typ: 'JWT' });
  return `${header}.${base64Url(claims)}.not-a-real-signature`;
}

/**
 * An `exp` value that many minutes from now. Negative for a token that has already died.
 * JWT measures this in *seconds* since the epoch, not milliseconds — mixing the two up
 * gives a token that expires in the year 57000, which is exactly the kind of bug these
 * tests exist to catch.
 */
export function expiringIn(minutes: number): number {
  return Math.floor(Date.now() / 1000) + minutes * 60;
}

function base64Url(value: unknown): string {
  // Encoded through UTF-8 bytes rather than handing btoa the string directly, because btoa
  // throws on anything above U+00FF. A Greek name in the payload has to survive this the
  // same way it survives the real decoder.
  const bytes = new TextEncoder().encode(JSON.stringify(value));
  const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join('');

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
