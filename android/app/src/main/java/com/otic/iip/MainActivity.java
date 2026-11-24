package com.otic.iip;

import android.Manifest;
import android.app.Activity;
import android.app.DownloadManager;
import android.app.KeyguardManager;
import android.content.ActivityNotFoundException;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Insets;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.view.WindowInsets;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.CookieManager;
import android.webkit.URLUtil;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

/**
 * The whole IIP app: one WebView on the IIP portal. Screens, data and business rules stay on the
 * server; a change to the website shows up when the page reloads. Rebuild the APK only when this
 * shell changes (package id, icon, name, start URL or WebView behaviour).
 */
public class MainActivity extends Activity {
    private static final int FILE_CHOOSER_REQUEST = 1001;
    private static final int STORAGE_PERMISSION_REQUEST = 1002;
    private static final int MICROPHONE_PERMISSION_REQUEST = 1003;
    private static final int NOTIFICATION_PERMISSION_REQUEST = 1004;
    private static final int WAKE_PERMISSION_REQUEST = 1005;
    /** Set by "Talk now" on a nudge and by the "Hey Kato" listener: open straight into a conversation. */
    static final String EXTRA_TALK = "com.otic.iip.TALK";
    private static final long LOCK_CHECK_MILLISECONDS = 2000;

    private WebView webView;
    private ValueCallback<Uri[]> filePathCallback;
    private PermissionRequest pendingMicrophoneRequest;

    /** True only while the WebView is showing IIP's own site: the bridge below does nothing for any other page. */
    private volatile boolean ownPage;
    private volatile boolean callOpen;
    private volatile boolean pendingTalk;
    private boolean lastLocked;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable lockCheck = new Runnable() {
        @Override
        public void run() {
            pushLockState(false);
            if (callOpen) {
                handler.postDelayed(this, LOCK_CHECK_MILLISECONDS);
            }
        }
    };
    private final BroadcastReceiver screenReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            pushLockState(false);
            if (callOpen) {
                // The phone takes a moment to lock after the screen goes off; look again shortly.
                handler.removeCallbacks(lockCheck);
                handler.postDelayed(lockCheck, 500);
            }
        }
    };
    private final BroadcastReceiver callButtonReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            String button = intent.getStringExtra(VoiceCallService.EXTRA_BUTTON);
            if (VoiceCallService.ACTION_MUTE_PRESSED.equals(button)) {
                dispatchToPage("iip:native-mute", "{}");
            } else if (VoiceCallService.ACTION_HANG_UP_PRESSED.equals(button)) {
                dispatchToPage("iip:native-hangup", "{}");
            }
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        webView = new WebView(this);
        // A WebView ignores its own padding when it lays the page out, so the page drew under the status bar.
        // The padding goes on a plain container around it instead, and the WebView fills what is left.
        FrameLayout frame = new FrameLayout(this);
        frame.setBackgroundColor(Color.WHITE);
        frame.addView(webView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        setContentView(frame);
        keepContentClearOfSystemBars(frame);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setMediaPlaybackRequiresUserGesture(false);

        // The site's session (and the Microsoft sign-in that sets it) must survive, so cookies are accepted.
        CookieManager cookies = CookieManager.getInstance();
        cookies.setAcceptCookie(true);
        cookies.setAcceptThirdPartyCookies(webView, true);

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                String ownHost = Uri.parse(BuildConfig.START_URL).getHost();
                String host = url == null ? null : Uri.parse(url).getHost();
                ownPage = ownHost != null && ownHost.equalsIgnoreCase(host);
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                if (ownPage) {
                    pushLockState(true);
                    if (pendingTalk) {
                        dispatchToPage("iip:native-talk", "{}");
                    }
                }
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                Uri uri = request.getUrl();
                String scheme = uri.getScheme();
                if ("http".equalsIgnoreCase(scheme) || "https".equalsIgnoreCase(scheme)) {
                    return false;
                }
                try {
                    startActivity(new Intent(Intent.ACTION_VIEW, uri));
                } catch (ActivityNotFoundException ignored) {
                    // Nothing on the phone handles this link; stay on the page.
                }
                return true;
            }
        });

        // Lets the page tap the phone as the live conversation starts and ends (see web/portal/lib/liveCues.ts).
        webView.addJavascriptInterface(new NativeBridge(), "IipNative");

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onPermissionRequest(PermissionRequest request) {
                runOnUiThread(() -> handleWebPermission(request));
            }

            @Override
            public void onPermissionRequestCanceled(PermissionRequest request) {
                if (pendingMicrophoneRequest == request) {
                    pendingMicrophoneRequest = null;
                }
            }

            @Override
            public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback, FileChooserParams params) {
                if (filePathCallback != null) {
                    filePathCallback.onReceiveValue(null);
                }
                filePathCallback = callback;
                try {
                    startActivityForResult(params.createIntent(), FILE_CHOOSER_REQUEST);
                } catch (ActivityNotFoundException e) {
                    filePathCallback = null;
                    Toast.makeText(MainActivity.this, "No file picker available", Toast.LENGTH_LONG).show();
                    return false;
                }
                return true;
            }
        });

        webView.setDownloadListener((url, userAgent, contentDisposition, mimeType, contentLength) ->
                startDownload(url, userAgent, contentDisposition, mimeType));

        IntentFilter screen = new IntentFilter();
        screen.addAction(Intent.ACTION_SCREEN_OFF);
        screen.addAction(Intent.ACTION_SCREEN_ON);
        screen.addAction(Intent.ACTION_USER_PRESENT);
        registerReceiver(screenReceiver, screen);
        IntentFilter buttons = new IntentFilter(VoiceCallService.ACTION_FROM_NOTIFICATION);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(callButtonReceiver, buttons, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(callButtonReceiver, buttons);
        }

        boolean fresh = savedInstanceState == null || webView.restoreState(savedInstanceState) == null;
        String opened = handleLaunchIntent(getIntent());
        if (fresh) {
            webView.loadUrl(opened != null ? opened : BuildConfig.START_URL);
        } else if (opened != null) {
            webView.loadUrl(opened);
        }
        NudgeCheck.schedule(this);
    }

    /**
     * What a launch asks for. "Talk now" and the wake phrase ask for a conversation; a nudge asks for one screen inside IIP.
     * Only an address on IIP's own site is ever loaded from an intent.
     */
    private String handleLaunchIntent(Intent intent) {
        if (intent == null) {
            return null;
        }
        if (intent.getBooleanExtra(EXTRA_TALK, false)) {
            pendingTalk = true;
            intent.removeExtra(EXTRA_TALK);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
                // Opened by the wake phrase on a locked phone: show over the lock screen. The conversation itself reads out
                // no figures while the phone is locked.
                setShowWhenLocked(true);
                setTurnScreenOn(true);
            }
        }
        Uri data = intent.getData();
        String ownHost = Uri.parse(BuildConfig.START_URL).getHost();
        if (data != null && ownHost != null && "https".equalsIgnoreCase(data.getScheme()) && ownHost.equalsIgnoreCase(data.getHost())) {
            return data.toString();
        }
        return null;
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        String opened = handleLaunchIntent(intent);
        if (opened != null) {
            webView.loadUrl(opened);
        } else if (pendingTalk) {
            dispatchToPage("iip:native-talk", "{}");
        }
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        WakeWordService.appInFront = hasFocus;
        // Android lets a service start the microphone only while the app is in front, so the "Hey Kato" listener is started
        // here, once the person has switched it on, and not while the phone is locked.
        if (hasFocus && !deviceLocked()) {
            WakeWordService.start(this);
        }
    }

    @Override
    protected void onStop() {
        super.onStop();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(false);
            setTurnScreenOn(false);
        }
    }

    /**
     * The page may use the microphone, and only the microphone, and only when it is IIP's own site. The phone's
     * own permission prompt appears the first time a person taps Talk or Record.
     */
    private void handleWebPermission(PermissionRequest request) {
        String ownHost = Uri.parse(BuildConfig.START_URL).getHost();
        boolean ownSite = ownHost != null && ownHost.equalsIgnoreCase(request.getOrigin().getHost());
        boolean onlyMicrophone = request.getResources().length == 1
                && PermissionRequest.RESOURCE_AUDIO_CAPTURE.equals(request.getResources()[0]);
        if (!ownSite || !onlyMicrophone) {
            request.deny();
            return;
        }
        if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
            request.grant(request.getResources());
            return;
        }
        pendingMicrophoneRequest = request;
        requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO}, MICROPHONE_PERMISSION_REQUEST);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == MICROPHONE_PERMISSION_REQUEST && pendingMicrophoneRequest != null) {
            PermissionRequest request = pendingMicrophoneRequest;
            pendingMicrophoneRequest = null;
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                request.grant(request.getResources());
            } else {
                request.deny();
            }
        }
        if (requestCode == WAKE_PERMISSION_REQUEST) {
            boolean allowed = grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED;
            WakeWordService.set(this, allowed);
            dispatchToPage("iip:native-settings", "{}");
        }
    }

    /**
     * Whether the phone is locked. This is the device lock (a PIN, pattern or fingerprint is needed to use it), not
     * whether the keyguard happens to be showing, which some makers report wrongly while an app covers it.
     */
    private boolean deviceLocked() {
        KeyguardManager keyguard = (KeyguardManager) getSystemService(Context.KEYGUARD_SERVICE);
        return keyguard != null && keyguard.isDeviceLocked();
    }

    /** Tells the page when the lock state changes (or always, when asked), so it can stop reading out figures. */
    private void pushLockState(boolean force) {
        boolean locked = deviceLocked();
        if (!force && locked == lastLocked) {
            return;
        }
        lastLocked = locked;
        dispatchToPage("iip:device-lock", "{\"locked\":" + locked + "}");
    }

    private void dispatchToPage(String name, String detailJson) {
        if (webView == null || !ownPage) {
            return;
        }
        String script = "window.dispatchEvent(new CustomEvent('" + name + "',{detail:" + detailJson + "}));";
        webView.post(() -> webView.evaluateJavascript(script, null));
    }

    private void callService(String action, Boolean muted) {
        Intent intent = new Intent(this, VoiceCallService.class).setAction(action);
        if (muted != null) {
            intent.putExtra(VoiceCallService.EXTRA_MUTED, muted.booleanValue());
        }
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(intent);
            } else {
                startService(intent);
            }
        } catch (RuntimeException e) {
            // Android refused to start a service from here (for example the app was not in front); the call
            // still works while the screen is on, it just is not kept alive in a pocket.
        }
    }

    /** Asked once, from the person's own tap on Talk: without it the call still runs but its notification is hidden. */
    private void askForNotificationsOnce() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU
                || checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED) {
            return;
        }
        SharedPreferences prefs = getSharedPreferences("iip", MODE_PRIVATE);
        if (prefs.getBoolean("asked_notifications", false)) {
            return;
        }
        prefs.edit().putBoolean("asked_notifications", true).apply();
        requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS}, NOTIFICATION_PERMISSION_REQUEST);
    }

    /** What the page may ask the phone to do. Every method does nothing unless the page is IIP's own site. */
    private final class NativeBridge {
        @JavascriptInterface
        public void callStarted() {
            if (!ownPage) {
                return;
            }
            callOpen = true;
            runOnUiThread(() -> {
                askForNotificationsOnce();
                callService(VoiceCallService.ACTION_START, null);
                handler.removeCallbacks(lockCheck);
                handler.post(lockCheck);
            });
        }

        @JavascriptInterface
        public void callEnded() {
            if (!ownPage || !callOpen) {
                return;
            }
            callOpen = false;
            runOnUiThread(() -> {
                handler.removeCallbacks(lockCheck);
                callService(VoiceCallService.ACTION_STOP, null);
            });
        }

        @JavascriptInterface
        public void callMuted(boolean muted) {
            if (!ownPage || !callOpen) {
                return;
            }
            runOnUiThread(() -> callService(VoiceCallService.ACTION_MUTE_STATE, muted));
        }

        @JavascriptInterface
        public boolean isLocked() {
            return ownPage && deviceLocked();
        }

        /** True once, after "Talk now" or the wake phrase asked for a conversation. */
        @JavascriptInterface
        public boolean takeTalk() {
            if (!ownPage) {
                return false;
            }
            boolean asked = pendingTalk;
            pendingTalk = false;
            return asked;
        }

        /** The server's verdict on the last wake phrase: pending, yes or no. */
        @JavascriptInterface
        public String wakeState() {
            if (!ownPage) {
                return "yes";
            }
            String verdict = WakeWordService.verdict;
            boolean recent = System.currentTimeMillis() - WakeWordService.verdictAt < 15_000L;
            return "pending".equals(verdict) || recent ? verdict : "yes";
        }

        @JavascriptInterface
        public boolean handsFreeAvailable() {
            return ownPage;
        }

        @JavascriptInterface
        public boolean wakeOn() {
            return ownPage && WakeWordService.on(MainActivity.this);
        }

        @JavascriptInterface
        public boolean nudgesOn() {
            return ownPage && NudgeCheck.on(MainActivity.this);
        }

        /** Switched on by the person in Settings; asks for the microphone first when it has not been allowed. */
        @JavascriptInterface
        public void setWake(boolean wanted) {
            if (!ownPage) {
                return;
            }
            runOnUiThread(() -> {
                if (wanted && checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                    requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO}, WAKE_PERMISSION_REQUEST);
                    return;
                }
                WakeWordService.set(MainActivity.this, wanted);
                dispatchToPage("iip:native-settings", "{}");
            });
        }

        @JavascriptInterface
        public void setNudges(boolean wanted) {
            if (!ownPage) {
                return;
            }
            runOnUiThread(() -> {
                if (wanted) {
                    askForNotificationsOnce();
                }
                NudgeCheck.set(MainActivity.this, wanted);
                dispatchToPage("iip:native-settings", "{}");
            });
        }

        /**
         * The signed-in page hands over the narrow credential for the two background checks (see DeviceCheck). Refused for any
         * page that is not IIP's own site and for an address that is not https.
         */
        @JavascriptInterface
        public boolean saveDeviceCheck(String token, String apiBase) {
            if (!ownPage || !DeviceCheck.save(MainActivity.this, token, apiBase)) {
                return false;
            }
            runOnUiThread(() -> NudgeCheck.schedule(MainActivity.this));
            return true;
        }

        /** True when this phone already holds the credential, so the page does not ask for another one. */
        @JavascriptInterface
        public boolean hasDeviceCheck() {
            return ownPage && DeviceCheck.present(MainActivity.this);
        }

        /**
         * On sign-out: forget the credential and stop the listener and the checks. Returns the credential that was held (empty
         * when none) so the page can have the server revoke it too; it is not kept anywhere after this.
         */
        @JavascriptInterface
        public String clearDeviceCheck() {
            if (!ownPage) {
                return "";
            }
            String held = DeviceCheck.token(MainActivity.this);
            DeviceCheck.clear(MainActivity.this);
            runOnUiThread(() -> {
                NudgeCheck.schedule(MainActivity.this);
                WakeWordService.set(MainActivity.this, false);
            });
            return held == null ? "" : held;
        }

        @JavascriptInterface
        @SuppressWarnings("deprecation")
        public void haptic(int milliseconds) {
            if (!ownPage) {
                return;
            }
            Vibrator vibrator = (Vibrator) getSystemService(VIBRATOR_SERVICE);
            if (vibrator == null || !vibrator.hasVibrator()) {
                return;
            }
            long length = Math.max(1, Math.min(milliseconds, 60));
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createOneShot(length, VibrationEffect.DEFAULT_AMPLITUDE));
            } else {
                vibrator.vibrate(length);
            }
        }
    }

    /**
     * On Android 15 an app that targets SDK 35 draws behind the status bar, the navigation bar and the
     * keyboard. Pad the page by those areas so the portal's header and composer are never covered.
     */
    private void keepContentClearOfSystemBars(View view) {
        view.setOnApplyWindowInsetsListener((v, insets) -> {
            int left;
            int top;
            int right;
            int bottom;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                Insets bars = insets.getInsets(
                        WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout() | WindowInsets.Type.ime());
                left = bars.left;
                top = bars.top;
                right = bars.right;
                bottom = bars.bottom;
            } else {
                left = insets.getSystemWindowInsetLeft();
                top = insets.getSystemWindowInsetTop();
                right = insets.getSystemWindowInsetRight();
                bottom = insets.getSystemWindowInsetBottom();
            }
            v.setPadding(left, top, right, bottom);
            return insets;
        });
    }

    private void startDownload(String url, String userAgent, String contentDisposition, String mimeType) {
        // A file the page builds in the browser (a blob or data link) has no address the system downloader can fetch.
        if (!URLUtil.isNetworkUrl(url)) {
            Toast.makeText(this, "This download is not supported in the app yet. Use the website in a browser.", Toast.LENGTH_LONG).show();
            return;
        }
        // Before Android 10 the public Downloads folder needs the storage permission, asked for here.
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q
                && checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE}, STORAGE_PERMISSION_REQUEST);
            Toast.makeText(this, "Allow storage, then tap the download again.", Toast.LENGTH_LONG).show();
            return;
        }

        String fileName = URLUtil.guessFileName(url, contentDisposition, mimeType);
        try {
            DownloadManager.Request request = new DownloadManager.Request(Uri.parse(url));
            request.setMimeType(mimeType);
            String cookie = CookieManager.getInstance().getCookie(url);
            if (cookie != null) {
                request.addRequestHeader("Cookie", cookie);
            }
            request.addRequestHeader("User-Agent", userAgent);
            request.setTitle(fileName);
            request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
            request.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, fileName);
            ((DownloadManager) getSystemService(DOWNLOAD_SERVICE)).enqueue(request);
            Toast.makeText(this, "Downloading " + fileName, Toast.LENGTH_SHORT).show();
        } catch (RuntimeException e) {
            Toast.makeText(this, "The download could not start.", Toast.LENGTH_LONG).show();
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        webView.saveState(outState);
    }

    @Override
    @SuppressWarnings("deprecation")
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == FILE_CHOOSER_REQUEST && filePathCallback != null) {
            filePathCallback.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(resultCode, data));
            filePathCallback = null;
        }
    }

    @Override
    @SuppressWarnings("deprecation")
    public void onBackPressed() {
        if (webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }

    @Override
    protected void onPause() {
        super.onPause();
        CookieManager.getInstance().flush();
    }

    @Override
    protected void onResume() {
        super.onResume();
        pushLockState(true);
    }

    @Override
    protected void onDestroy() {
        handler.removeCallbacks(lockCheck);
        try {
            unregisterReceiver(screenReceiver);
            unregisterReceiver(callButtonReceiver);
        } catch (IllegalArgumentException ignored) {
            // Already unregistered.
        }
        if (callOpen) {
            // The page that held the call is going away, so the call cannot continue: stop the notification too.
            callOpen = false;
            callService(VoiceCallService.ACTION_STOP, null);
        }
        super.onDestroy();
    }
}
