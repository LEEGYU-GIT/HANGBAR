// Test-only signing key. Not a secret: used only to sign tokens in tests.
export const TEST_PRIVATE_JWK = {"kty":"EC","x":"wP_6IxcFKKO34wBeTnmYTt7-Sc48f4IMSZ-RRX-Urwk","y":"nV168edLbGoqerbu8-ThTu19G99bMHP5-KJNlaQqlz4","crv":"P-256","d":"oOYa-3U6D6X79d2PwxTcvMdKfjyN9wpFnJ9xKxM2ri8","kid":"test","alg":"ES256"} as const;
export const TEST_JWKS = {"keys":[{"kty":"EC","x":"wP_6IxcFKKO34wBeTnmYTt7-Sc48f4IMSZ-RRX-Urwk","y":"nV168edLbGoqerbu8-ThTu19G99bMHP5-KJNlaQqlz4","crv":"P-256","kid":"test","alg":"ES256"}]} as const;
