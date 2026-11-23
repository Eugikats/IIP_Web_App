package com.otic.iip;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.pm.ServiceInfo;
import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.os.Build;
import android.os.IBinder;
import android.os.PowerManager;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.util.Log;

import com.k2fsa.sherpa.onnx.FeatureConfig;
import com.k2fsa.sherpa.onnx.KeywordSpotter;
import com.k2fsa.sherpa.onnx.KeywordSpotterConfig;
import com.k2fsa.sherpa.onnx.OnlineModelConfig;
import com.k2fsa.sherpa.onnx.OnlineStream;
import com.k2fsa.sherpa.onnx.OnlineTransducerModelConfig;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.nio.charset.StandardCharsets;

/**
 * "Hey Kato": while the person has switched it on, this foreground service listens for those two words with a small
 * speech model that runs on the phone (sherpa-onnx keyword spotting). Nothing leaves the phone while it listens. When
 * the phone thinks it heard the words it sends just those two or three seconds to IIP, which writes them down with
 * Azure Speech and confirms that the words really were "Hey Kato"; then IIP opens into a conversation. A locked phone
 * still opens it, and the conversation then reads out no figures until the phone is unlocked.
 *
 * It is off until the person turns it on in Settings, and it steps aside (and frees the microphone) while a
 * conversation is running or the page is recording, so the page's own microphone always works.
 */
public class WakeWordService extends Service {
    static final String CHANNEL = "iip-hey-kato-listening";
    static final String WAKE_CHANNEL = "iip-hey-kato-wake";
    static final int NOTE_ID = 7310;
    static final int WAKE_NOTE_ID = 7311;
    static final String ACTION_OFF = "com.otic.iip.WAKE_OFF";
    static final String PREF = "wakeWordOn";
    static final int RATE = 16000;

    /** The server's verdict on the last wake, read by the page: pending, yes or no. */
    static volatile String verdict = "yes";
    /** When the verdict last changed, so an old "no" is never held against a conversation that was started another way. */
    static volatile long verdictAt;
    /** IIP is on screen. */
    static volatile boolean appInFront;
    /** The page is using the microphone (a voice note); the listener steps aside. */
    static volatile boolean pageMic;
    static volatile boolean running;

    private volatile boolean stop;
    private Thread worker;
    private PowerManager.WakeLock lock;

    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("iip", MODE_PRIVATE);
    }

    /** Off until the person turns it on. */
    static boolean on(Context context) {
        return prefs(context).getBoolean(PREF, false);
    }

    /** Switched on or off from Settings in the page. Starting needs the app in front (Android's rule for the microphone). */
    static void set(Context context, boolean wanted) {
        prefs(context).edit().putBoolean(PREF, wanted).apply();
        if (wanted) {
            start(context);
        } else {
            context.stopService(new Intent(context, WakeWordService.class));
        }
    }

    static void start(Context context) {
        if (!on(context) || running) {
            return;
        }
        if (context.checkSelfPermission(android.Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            return;
        }
        try {
            Intent intent = new Intent(context, WakeWordService.class);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent);
            } else {
                context.startService(intent);
            }
        } catch (RuntimeException ignored) {
            // Android refused (the app was not in front); it starts the next time the app opens.
        }
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_OFF.equals(intent.getAction())) {
            prefs(this).edit().putBoolean(PREF, false).apply();
            stopSelf();
            return START_NOT_STICKY;
        }
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                startForeground(NOTE_ID, note(), ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE);
            } else {
                startForeground(NOTE_ID, note());
            }
        } catch (RuntimeException e) {
            stopSelf();
            return START_NOT_STICKY;
        }
        running = true;
        if (worker == null) {
            stop = false;
            PowerManager power = (PowerManager) getSystemService(POWER_SERVICE);
            if (power != null) {
                lock = power.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "iip:hey-kato");
                lock.acquire();
            }
            worker = new Thread(this::listen, "kato-wake");
            worker.start();
        }
        // Not sticky: restarted by Android in the background it could not use the microphone.
        return START_NOT_STICKY;
    }

    @Override
    public void onDestroy() {
        stop = true;
        running = false;
        if (worker != null) {
            worker.interrupt();
        }
        if (lock != null && lock.isHeld()) {
            lock.release();
        }
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    // ------------------------------------------------------------------ listening

    private KeywordSpotter spotter() {
        OnlineTransducerModelConfig transducer = new OnlineTransducerModelConfig();
        transducer.setEncoder("wake/encoder.onnx");
        transducer.setDecoder("wake/decoder.onnx");
        transducer.setJoiner("wake/joiner.onnx");
        OnlineModelConfig model = new OnlineModelConfig();
        model.setTransducer(transducer);
        model.setTokens("wake/tokens.txt");
        model.setNumThreads(1);
        model.setProvider("cpu");
        // Loose on purpose: the second check on the server removes near misses. These thresholds have NOT been tuned for
        // "Hey Kato" on real phones with real voices; see android/README.md before relying on them.
        KeywordSpotterConfig config = new KeywordSpotterConfig();
        config.setFeatConfig(new FeatureConfig(RATE, 80, 0f));
        config.setModelConfig(model);
        config.setMaxActivePaths(8);
        config.setKeywordsFile("wake/keywords.txt");
        config.setKeywordsScore(2.0f);
        config.setKeywordsThreshold(0.05f);
        config.setNumTrailingBlanks(1);
        return new KeywordSpotter(getAssets(), config);
    }

    private void listen() {
        KeywordSpotter kws;
        try {
            kws = spotter();
        } catch (Throwable e) {
            Log.w("IipKato", "the keyword model could not start: " + e.getClass().getSimpleName());
            stopSelf();
            return;
        }
        OnlineStream stream = kws.createStream("");
        short[] ring = new short[RATE * 3];          // the last three seconds, for the second check
        int pos = 0;
        long quietUntil = 0;
        short[] buffer = new short[RATE / 10];
        float gain = 1f;
        float[] floats = new float[buffer.length];
        AudioRecord recorder = null;
        int refused = 0;
        boolean wasHeld = false;
        try {
            while (!stop) {
                // Steps aside while the page records or a conversation is running. The page can only record while it is on
                // screen, so its flag counts only then: a flag left set by a call that never started cannot hold the listener.
                boolean held = (pageMic && appInFront) || VoiceCallService.running;
                if (held != wasHeld) {
                    Log.i("IipKato", held ? "paused" : "resuming");
                    wasHeld = held;
                }
                if (held) {
                    if (recorder != null) {
                        recorder.stop();
                        recorder.release();
                        recorder = null;
                        kws.reset(stream);
                    }
                    if (lock != null && lock.isHeld()) {
                        lock.release();      // nothing to hear: let the phone sleep
                    }
                    Thread.sleep(500);
                    continue;
                }
                if (recorder == null) {
                    if (lock != null && !lock.isHeld()) {
                        lock.acquire();
                    }
                    int min = AudioRecord.getMinBufferSize(RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT);
                    recorder = new AudioRecord(MediaRecorder.AudioSource.VOICE_RECOGNITION, RATE, AudioFormat.CHANNEL_IN_MONO,
                            AudioFormat.ENCODING_PCM_16BIT, Math.max(min, RATE));
                    if (recorder.getState() != AudioRecord.STATE_INITIALIZED) {
                        recorder.release();
                        recorder = null;
                        // Android lets a background listener use the microphone only when it was started while the app was
                        // on screen. Refused: stop, so the next time the app opens it starts properly.
                        if (++refused >= 3) {
                            Log.w("IipKato", "microphone refused; waiting for the app to open");
                            break;
                        }
                        Thread.sleep(2000);
                        continue;
                    }
                    refused = 0;
                    recorder.startRecording();
                    Log.i("IipKato", "listening");
                }
                int n = recorder.read(buffer, 0, buffer.length);
                if (n <= 0) {
                    Thread.sleep(100);
                    continue;
                }
                // Lift quiet speech (someone across the room) to a steady level before the spotter hears it. The clip sent
                // for the check stays as recorded.
                double energy = 0;
                for (int i = 0; i < n; i++) {
                    energy += (double) buffer[i] * buffer[i];
                    ring[pos] = buffer[i];
                    pos = (pos + 1) % ring.length;
                }
                double rms = Math.sqrt(energy / n) / 32768.0;
                if (rms > 0.003) {
                    gain = 0.8f * gain + 0.2f * (float) Math.min(30.0, 0.1 / rms);
                }
                for (int i = 0; i < n; i++) {
                    floats[i] = Math.max(-1f, Math.min(1f, buffer[i] / 32768f * gain));
                }
                stream.acceptWaveform(n == floats.length ? floats : java.util.Arrays.copyOf(floats, n), RATE);
                boolean heard = false;
                while (kws.isReady(stream)) {
                    kws.decode(stream);
                    if (!kws.getResult(stream).getKeyword().isEmpty()) {
                        heard = true;
                        kws.reset(stream);
                    }
                }
                if (!heard || System.currentTimeMillis() < quietUntil) {
                    continue;
                }
                Log.i("IipKato", "heard the phrase");
                // Two tenths of a second more, so the end of "Kato" is in the clip.
                for (int k = 0; k < 2; k++) {
                    int more = recorder.read(buffer, 0, buffer.length);
                    for (int i = 0; i < more; i++) {
                        ring[pos] = buffer[i];
                        pos = (pos + 1) % ring.length;
                    }
                }
                short[] clip = new short[ring.length];
                for (int i = 0; i < ring.length; i++) {
                    clip[i] = ring[(pos + i) % ring.length];
                }
                // Heard clearly: open at once (as phone assistants do) and check the words at the same time. The page holds
                // the voice and the microphone until the check is back, and steps away on a "no".
                verdict = "pending";
                verdictAt = System.currentTimeMillis();
                recorder.stop();
                recorder.release();
                recorder = null;            // the conversation needs the microphone
                kws.reset(stream);
                wake();
                new Thread(() -> {
                    boolean ok = confirmed(clip);
                    verdict = ok ? "yes" : "no";
                    verdictAt = System.currentTimeMillis();
                    Log.i("IipKato", "checked: " + verdict);
                }, "kato-check").start();
                quietUntil = System.currentTimeMillis() + 8000;
            }
        } catch (InterruptedException ignored) {
            // asked to stop
        } catch (Throwable e) {
            // the microphone was taken away or failed: stop quietly; opening the app starts it again
        } finally {
            if (recorder != null) {
                try {
                    recorder.stop();
                } catch (RuntimeException ignored) {
                    // already stopped
                }
                recorder.release();
            }
            stream.release();
            kws.release();
            if (!stop) {
                stopSelf();
            }
        }
    }

    /**
     * IIP's second check: the clip is written down and the answer says whether it really was "Hey Kato". A person who is
     * not registered with the server (signed out, credential revoked or expired) never opens it. When the server cannot
     * be reached or is busy, the phone's own clear match stands.
     */
    private boolean confirmed(short[] pcm) {
        try {
            String boundary = "----kato" + System.nanoTime();
            ByteArrayOutputStream body = new ByteArrayOutputStream();
            body.write(("--" + boundary + "\r\nContent-Disposition: form-data; name=\"audio\"; filename=\"wake.wav\"\r\n"
                    + "Content-Type: audio/wav\r\n\r\n").getBytes(StandardCharsets.US_ASCII));
            body.write(wav(pcm));
            body.write(("\r\n--" + boundary + "--\r\n").getBytes(StandardCharsets.US_ASCII));
            HttpURLConnection connection = DeviceCheck.open(this, "/api/v1/voice/device/wake", "POST", 5000, 8000);
            if (connection == null) {
                Log.i("IipKato", "check: not registered");
                return false;
            }
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "multipart/form-data; boundary=" + boundary);
            try (OutputStream out = connection.getOutputStream()) {
                body.writeTo(out);
            }
            int code = connection.getResponseCode();
            if (code == 401) {
                Log.i("IipKato", "check: the credential is no longer valid");
                DeviceCheck.clear(this);
                return false;
            }
            if (code != 200) {
                Log.i("IipKato", "check failed: HTTP " + code);
                try (InputStream error = connection.getErrorStream()) {
                    if (error != null) {
                        while (error.read() >= 0) {
                            // drain, so the connection can be reused
                        }
                    }
                }
                return true;
            }
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            try (InputStream in = connection.getInputStream()) {
                byte[] chunk = new byte[4096];
                for (int n; (n = in.read(chunk)) > 0; ) {
                    out.write(chunk, 0, n);
                }
            }
            return new JSONObject(out.toString("UTF-8")).optBoolean("wake", false);
            // no disconnect(): the connection is kept for the next request, which saves the secure handshake
        } catch (Exception e) {
            Log.i("IipKato", "check failed: " + e.getClass().getSimpleName());
            return true;
        }
    }

    private static byte[] wav(short[] pcm) {
        int data = pcm.length * 2;
        java.nio.ByteBuffer bb = java.nio.ByteBuffer.allocate(44 + data).order(java.nio.ByteOrder.LITTLE_ENDIAN);
        bb.put("RIFF".getBytes(StandardCharsets.US_ASCII)).putInt(36 + data).put("WAVE".getBytes(StandardCharsets.US_ASCII))
                .put("fmt ".getBytes(StandardCharsets.US_ASCII)).putInt(16).putShort((short) 1).putShort((short) 1)
                .putInt(RATE).putInt(RATE * 2).putShort((short) 2).putShort((short) 16)
                .put("data".getBytes(StandardCharsets.US_ASCII)).putInt(data);
        for (short s : pcm) {
            bb.putShort(s);
        }
        return bb.array();
    }

    // ------------------------------------------------------------------ opening IIP

    /** Opens IIP into a conversation: straight away when it is on screen, otherwise by a full-screen alert, as a call does. */
    private void wake() {
        try {
            Vibrator vibrator = (Vibrator) getSystemService(VIBRATOR_SERVICE);
            if (vibrator != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createOneShot(40, 120));
            }
        } catch (RuntimeException ignored) {
            // no vibration
        }
        Intent open = new Intent(this, MainActivity.class)
                .putExtra(MainActivity.EXTRA_TALK, true)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        PendingIntent pending = PendingIntent.getActivity(this, 21, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        if (appInFront) {
            try {
                pending.send();
                return;
            } catch (PendingIntent.CanceledException ignored) {
                // fall through to the alert
            }
        }
        NotificationManager manager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && manager != null && manager.getNotificationChannel(WAKE_CHANNEL) == null) {
            NotificationChannel channel = new NotificationChannel(WAKE_CHANNEL, "Hey Kato", NotificationManager.IMPORTANCE_HIGH);
            channel.setDescription("Opens IIP when you say Hey Kato");
            channel.setSound(null, null);
            channel.enableVibration(false);
            channel.setShowBadge(false);
            // Public on a locked phone: it says only that IIP heard you, nothing about the business.
            channel.setLockscreenVisibility(Notification.VISIBILITY_PUBLIC);
            manager.createNotificationChannel(channel);
        }
        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, WAKE_CHANNEL)
                : new Notification.Builder(this);
        builder.setSmallIcon(android.R.drawable.ic_btn_speak_now)
                .setContentTitle("Kato")
                .setContentText("I heard you. Tap to talk.")
                .setCategory(Notification.CATEGORY_CALL)
                .setVisibility(Notification.VISIBILITY_PUBLIC)
                .setAutoCancel(true)
                .setTimeoutAfter(20000)
                .setContentIntent(pending)
                .setFullScreenIntent(pending, true);
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            builder.setPriority(Notification.PRIORITY_HIGH);
        }
        try {
            if (manager != null) {
                manager.notify(WAKE_NOTE_ID, builder.build());
            }
        } catch (SecurityException ignored) {
            // notifications not allowed
        }
        try {
            pending.send();      // allowed when the app was opened recently or the screen is off; otherwise the alert opens it
        } catch (PendingIntent.CanceledException ignored) {
            // the alert above is the way in
        }
    }

    private Notification note() {
        NotificationManager manager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && manager != null && manager.getNotificationChannel(CHANNEL) == null) {
            NotificationChannel channel = new NotificationChannel(CHANNEL, "Listening for Hey Kato", NotificationManager.IMPORTANCE_MIN);
            channel.setDescription("Shown while the phone listens for Hey Kato");
            channel.setShowBadge(false);
            manager.createNotificationChannel(channel);
        }
        int immutable = PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE;
        PendingIntent open = PendingIntent.getActivity(this, 22, new Intent(this, MainActivity.class), immutable);
        PendingIntent off = PendingIntent.getService(this, 23, new Intent(this, WakeWordService.class).setAction(ACTION_OFF), immutable);
        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL)
                : new Notification.Builder(this);
        builder.setSmallIcon(android.R.drawable.ic_btn_speak_now)
                .setContentTitle("Say Hey Kato to talk")
                .setContentText("The phone listens for those words only. Nothing is sent until it hears them.")
                .setOngoing(true)
                .setShowWhen(false)
                .setContentIntent(open)
                .addAction(new Notification.Action.Builder(null, "Turn off", off).build());
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            builder.setPriority(Notification.PRIORITY_MIN);
        }
        return builder.build();
    }
}
