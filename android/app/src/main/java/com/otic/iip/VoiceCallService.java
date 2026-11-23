package com.otic.iip;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;
import android.os.PowerManager;

/**
 * Keeps a live conversation going while the phone is in a pocket or the screen is off. Android stops a page's
 * microphone as soon as the app is no longer the thing on screen unless a foreground service of type microphone
 * is running, and it requires that service to show a notification the person can see.
 *
 * The service holds no audio and talks to no server. The call itself still runs in the page; this only keeps the
 * app alive and gives the person Mute and Hang up on the notification. Those two buttons are sent to the page
 * through {@link #ACTION_FROM_NOTIFICATION}, which {@link MainActivity} turns into events the page listens for.
 */
public class VoiceCallService extends Service {
    public static final String ACTION_START = "com.otic.iip.call.START";
    public static final String ACTION_STOP = "com.otic.iip.call.STOP";
    public static final String ACTION_MUTE_STATE = "com.otic.iip.call.MUTE_STATE";
    public static final String ACTION_MUTE_PRESSED = "com.otic.iip.call.MUTE_PRESSED";
    public static final String ACTION_HANG_UP_PRESSED = "com.otic.iip.call.HANG_UP_PRESSED";
    /** Sent to this app only, to the activity, when a notification button is pressed. */
    public static final String ACTION_FROM_NOTIFICATION = "com.otic.iip.call.FROM_NOTIFICATION";
    public static final String EXTRA_BUTTON = "button";
    public static final String EXTRA_MUTED = "muted";

    private static final String CHANNEL_ID = "iip_live_call";
    private static final int NOTIFICATION_ID = 4101;
    /** The longest a call can last on the server is 15 minutes; the lock is released a little after that at the latest. */
    private static final long WAKE_LOCK_MILLISECONDS = 16L * 60L * 1000L;

    /** A conversation is being held open; the "Hey Kato" listener steps aside while this is true. */
    static volatile boolean running;

    private boolean muted;
    private PowerManager.WakeLock wakeLock;

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String action = intent == null ? null : intent.getAction();
        if (action == null) {
            // Restarted by the system without a call to hold: there is nothing to keep going.
            stopSelf();
            return START_NOT_STICKY;
        }
        switch (action) {
            case ACTION_START:
                running = true;
                muted = false;
                showNotification();
                holdCpu();
                break;
            case ACTION_MUTE_STATE:
                muted = intent.getBooleanExtra(EXTRA_MUTED, false);
                showNotification();
                break;
            case ACTION_MUTE_PRESSED:
                sendToPage(ACTION_MUTE_PRESSED);
                break;
            case ACTION_HANG_UP_PRESSED:
                sendToPage(ACTION_HANG_UP_PRESSED);
                // If the page is gone there is nobody to end the call for; the notification must not linger.
                endService();
                break;
            case ACTION_STOP:
            default:
                endService();
                break;
        }
        return START_NOT_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        running = false;
        releaseCpu();
        super.onDestroy();
    }

    private void endService() {
        running = false;
        releaseCpu();
        stopForeground(true);
        stopSelf();
    }

    private void sendToPage(String button) {
        Intent forward = new Intent(ACTION_FROM_NOTIFICATION);
        forward.setPackage(getPackageName());
        forward.putExtra(EXTRA_BUTTON, button);
        sendBroadcast(forward);
    }

    private void showNotification() {
        NotificationManager manager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && manager != null) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID, "Live conversation", NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("Shown while a live conversation with IIP is open.");
            channel.setShowBadge(false);
            manager.createNotificationChannel(channel);
        }

        Intent open = new Intent(this, MainActivity.class)
                .setFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        int immutable = Build.VERSION.SDK_INT >= Build.VERSION_CODES.M ? PendingIntent.FLAG_IMMUTABLE : 0;
        PendingIntent openIntent = PendingIntent.getActivity(this, 0, open, immutable);

        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        builder.setSmallIcon(android.R.drawable.ic_btn_speak_now)
                .setContentTitle("IIP live conversation")
                .setContentText(muted ? "Muted. IIP is not listening." : "IIP is listening.")
                .setContentIntent(openIntent)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .addAction(action(muted ? "Unmute" : "Mute", ACTION_MUTE_PRESSED, 1))
                .addAction(action("Hang up", ACTION_HANG_UP_PRESSED, 2));
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, builder.build(), ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE);
        } else {
            startForeground(NOTIFICATION_ID, builder.build());
        }
    }

    private Notification.Action action(String label, String intentAction, int requestCode) {
        Intent intent = new Intent(this, VoiceCallService.class).setAction(intentAction);
        int immutable = Build.VERSION.SDK_INT >= Build.VERSION_CODES.M ? PendingIntent.FLAG_IMMUTABLE : 0;
        PendingIntent pending = PendingIntent.getService(this, requestCode, intent, immutable);
        return new Notification.Action.Builder(null, label, pending).build();
    }

    private void holdCpu() {
        if (wakeLock != null && wakeLock.isHeld()) {
            return;
        }
        PowerManager power = (PowerManager) getSystemService(Context.POWER_SERVICE);
        if (power == null) {
            return;
        }
        wakeLock = power.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "iip:live-call");
        wakeLock.setReferenceCounted(false);
        wakeLock.acquire(WAKE_LOCK_MILLISECONDS);
    }

    private void releaseCpu() {
        if (wakeLock != null && wakeLock.isHeld()) {
            wakeLock.release();
        }
        wakeLock = null;
    }
}
