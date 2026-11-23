package com.otic.iip;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;

import java.io.IOException;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * The narrow credential the phone uses for its two background checks while the app is closed (is there a nudge, and
 * was that really "Hey Kato"). It is not the person's sign-in: the portal's own token lives only in an open tab. The
 * signed-in page asks the API for this credential and hands it over; it is kept in this app's private storage (the
 * app does not allow backup, so it never reaches a cloud copy), can do nothing but those two checks, and is revoked
 * when the person signs out.
 */
final class DeviceCheck {
    static final String HEADER = "X-Device-Check";
    private static final String PREFS = "iip-device";

    private DeviceCheck() {}

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    /** Only an https address is kept, so the credential is never sent in the clear. */
    static boolean save(Context context, String token, String apiBase) {
        if (token == null || token.length() < 20 || token.length() > 128 || apiBase == null) {
            return false;
        }
        Uri uri = Uri.parse(apiBase);
        boolean https = "https".equalsIgnoreCase(uri.getScheme()) && uri.getHost() != null;
        boolean localhost = "http".equalsIgnoreCase(uri.getScheme()) && "10.0.2.2".equals(uri.getHost()) && BuildConfig.DEBUG;
        if (!https && !localhost) {
            return false;
        }
        String base = apiBase.endsWith("/") ? apiBase.substring(0, apiBase.length() - 1) : apiBase;
        prefs(context).edit().putString("token", token).putString("api", base).apply();
        return true;
    }

    static void clear(Context context) {
        prefs(context).edit().clear().apply();
    }

    static boolean present(Context context) {
        return token(context) != null && apiBase(context) != null;
    }

    static String token(Context context) {
        return prefs(context).getString("token", null);
    }

    static String apiBase(Context context) {
        return prefs(context).getString("api", null);
    }

    /** A connection to one of the two device endpoints with the credential attached, or null when none is held. */
    static HttpURLConnection open(Context context, String path, String method, int connectMs, int readMs) throws IOException {
        String token = token(context);
        String base = apiBase(context);
        if (token == null || base == null) {
            return null;
        }
        HttpURLConnection connection = (HttpURLConnection) new URL(base + path).openConnection();
        connection.setConnectTimeout(connectMs);
        connection.setReadTimeout(readMs);
        connection.setRequestMethod(method);
        connection.setInstanceFollowRedirects(false);
        connection.setRequestProperty(HEADER, token);
        connection.setRequestProperty("Accept", "application/json");
        return connection;
    }
}
