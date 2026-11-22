import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";
import { TEST_JWKS } from "./test/keys.ts";

export const TEST_SUPABASE_URL = "https://test.supabase.co";

export default defineConfig({
  plugins: [
    cloudflareTest({
      wrangler: { configPath: "./wrangler.jsonc" },
      miniflare: {
        bindings: {
          SUPABASE_URL: TEST_SUPABASE_URL,
          JWKS_JSON: JSON.stringify(TEST_JWKS),
        },
      },
    }),
  ],
});
