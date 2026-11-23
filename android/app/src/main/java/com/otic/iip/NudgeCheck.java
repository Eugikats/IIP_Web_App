package com.otic.iip;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.KeyguardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.util.Calendar;
import java.util.TimeZone;

/**
 * Nudges while the app is closed: about every 40 minutes the phone asks IIP whether anything needs the person, using
 * its narrow device credential (see {@link DeviceCheck}), and shows one notification when there is. IIP decides whether
 * to say anything (a few a day, working hours, never the same thing twice in three days), so a check that finds nothing
 * is a plain 204. On a locked phone the notification says only that something needs attention: no names, no figures.
 */
final class NudgeCheck {
    static final String CHANNEL = "iip-nudges";
    static final String ACTION_CHECK = "com.otic.iip.NUDGE_CHECK";
    static final int NOTE_ID = 7320;
    private static final long EVERY_MILLISECONDS = 40L * 60L * 1000L;
    private static final TimeZone KAMPALA = TimeZone.getTimeZone("Africa/Kampala");

    private NudgeCheck() {}

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("iip", Context.MODE_PRIVATE);
    }

    /** On unless the person switched it off in Settings. */
    static boolean on(Context context) {
        return prefs(context).getBoolean("nudgesOn", true);
    }

    static void set(Context context, boolean wanted) {
        prefs(context).edit().putBoolean("nudgesOn", wanted).apply();
        schedule(context);
    }

    private static PendingIntent alarm(Context context) {
        Intent intent = new Intent(context, NudgeReceiver.class).setAction(ACTION_CHECK);
        return PendingIntent.getBroadcast(context, 31, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    /** Sets the next check, or clears it when nudges are off or the phone holds no credential. */
    static void schedule(Context context) {
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms == null) {
            return;
        }
        PendingIntent pending = alarm(context);
        alarms.cancel(pending);
        if (!on(context) || !DeviceCheck.present(context)) {
            return;
        }
        alarms.setAndAllowWhileIdle(AlarmManager.RTC, System.currentTimeMillis() + EVERY_MILLISECONDS, pending);
    }

    /** Weekdays, 08:00 to 18:00 in Kampala: the same window the server keeps to, so a night-time check costs nothing. */
    static boolean inWorkingHours() {
        Calendar now = Calendar.getInstance(KAMPALA);
        int day = now.get(Calendar.DAY_OF_WEEK);
        int hour = now.get(Calendar.HOUR_OF_DAY);
        return day != Calendar.SATURDAY && day != Calendar.SUNDAY && hour >= 8 && hour < 18;
    }

    /** One check, on the calling thread. Returns normally whatever happens; the next check is set by the receiver. */
    static void run(Context context) {
        if (!on(context) || !DeviceCheck.present(context) || !inWorkingHours()) {
            return;
        }
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null || !manager.areNotificationsEnabled()) {
            return;
        }
        KeyguardManager keyguard = (KeyguardManager) context.getSystemService(Context.KEYGUARD_SERVICE);
        boolean locked = keyguard != null && keyguard.isDeviceLocked();
        try {
            HttpURLConnection connection = DeviceCheck.open(context, "/api/v1/voice/device/nudge?locked=" + locked, "GET", 8000, 10000);
            if (connection == null) {
                return;
            }
            int code = connection.getResponseCode();
            if (code == 401) {
                DeviceCheck.clear(context);      // revoked or expired: the app asks for a new one when it is next opened
                return;
            }
            if (code != 200) {
                connection.disconnect();
                return;
            }
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            try (InputStream in = connection.getInputStream()) {
                byte[] chunk = new byte[4096];
                for (int n; (n = in.read(chunk)) > 0; ) {
                    out.write(chunk, 0, n);
                }
            }
            connection.disconnect();
            JSONObject nudge = new JSONObject(out.toString("UTF-8"));
            String text = nudge.optString("text", "");
            if (!text.isEmpty()) {
                notify(context, manager, text, nudge.isNull("href") ? null : nudge.optString("href", null));
            }
        } catch (Exception ignored) {
            // offline: nothing is shown and the next check tries again
        }
    }

    private static void notify(Context context, NotificationManager manager, String text, String href) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && manager.getNotificationChannel(CHANNEL) == null) {
            NotificationChannel channel = new NotificationChannel(CHANNEL, "Needs you", NotificationManager.IMPORTANCE_DEFAULT);
            channel.setDescription("A short note when something in IIP needs you");
            manager.createNotificationChannel(channel);
        }
        int immutable = PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE;
        Intent open = new Intent(context, MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        // Only a screen inside IIP is ever opened from a nudge.
        if (href != null && href.startsWith("/") && !href.startsWith("//") && !href.contains("\\")) {
            Uri start = Uri.parse(BuildConfig.START_URL);
            open.setData(Uri.parse(start.getScheme() + "://" + start.getAuthority() + href));
        }
        PendingIntent openPending = PendingIntent.getActivity(context, 32, open, immutable);
        Intent talk = new Intent(context, MainActivity.class).putExtra(MainActivity.EXTRA_TALK, true)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        PendingIntent talkPending = PendingIntent.getActivity(context, 33, talk, immutable);

        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(context, CHANNEL)
                : new Notification.Builder(context);
        builder.setSmallIcon(android.R.drawable.ic_btn_speak_now)
                .setContentTitle("Needs you")
                .setContentText(text)
                .setStyle(new Notification.BigTextStyle().bigText(text))
                .setAutoCancel(true)
                .setContentIntent(openPending)
                .addAction(new Notification.Action.Builder(null, "Talk now", talkPending).build());
        try {
            manager.notify(NOTE_ID, builder.build());
        } catch (SecurityException ignored) {
            // notifications not allowed
        }
    }
}
