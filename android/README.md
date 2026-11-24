# IIP for Android

A native Android app that is only a window onto the IIP portal. The screens, data and business rules stay on the server, so a change to the portal shows up when the installed app reloads. A new APK is needed only when the shell changes: package id, icon, app name, start URL or WebView behaviour.

| | |
|---|---|
| App name | `IIP` |
| Package id | `com.otic.iip` (do not change after the first install: a new id installs a second app) |
| Start URL | `BuildConfig.START_URL` in `app/build.gradle`, currently the dev portal, `/app/workspace` |
| Build | one Java `Activity`, one `WebView`, no app dependencies, minSdk 26, targetSdk 35 |

The start URL points at the `dev` environment (`app-portal-dev-h5girbnzrayqe`). Change it in `app/build.gradle` and raise `versionCode` when a production portal exists.

## Build

From this folder, with Gradle 8.9, JDK 17 or newer (Android Studio's JBR is JDK 21) and the Android SDK with build-tools `35.0.0`:

```bash
JAVA_HOME="C:/Program Files/Android/Android Studio/jbr" ANDROID_HOME="$LOCALAPPDATA/Android/Sdk" gradle assembleRelease
```

**Without Gradle:** `android/build.sh` builds `build/iip.apk` with the SDK build tools alone (aapt2, javac, d8, zipalign, apksigner) plus Python 3, curl, tar and unzip, the way the sibling MIP app is built. Set `JAVA_HOME` and `ANDROID_HOME` first. The first run downloads the sherpa-onnx library (Apache-2.0) and the keyword model into `build/wake-cache`, checking each against a pinned SHA-256. It signs with the debug key, so it installs over a Gradle build. The Gradle file still lists no dependencies, so a Gradle build needs the sherpa-onnx `.aar` and `kotlin-stdlib` added by hand and the `wake` assets copied in; use `build.sh` instead.

The Gradle APK is `app/build/outputs/apk/release/app-release.apk`. Copy it to the repo root as `IIP.apk` to sideload it (not committed). The release build is signed with the debug key so it installs without a keystore, which is fine for sideloading and not for the Play Store. Raise `versionCode` for every APK handed to someone who already has the app installed. Confirm a build with `aapt dump badging IIP.apk`.

## What the shell does

- Keeps `http` and `https` links inside the app and sends every other scheme to the phone.
- Accepts cookies, so the Microsoft sign-in session survives. The portal signs in by redirect, which works in a WebView.
- File upload through the system picker; downloads through the system downloader, with the session cookie.
- Pads the page by the status bar, navigation bar and keyboard, because an app targeting SDK 35 otherwise draws underneath them.
- Microphone for the portal's voice note and live conversation: asked for only when a person taps Record or Talk, granted only to IIP's own site and only for audio capture. A small `IipNative.haptic(ms)` bridge lets the page tap the phone (clamped to 60 ms). `versionCode` 2 (1.1.0) added this.
- A live conversation keeps going with the phone in a pocket: while the page has a call open a foreground service of type microphone (`VoiceCallService`) shows a notification with Mute and Hang up, and holds the CPU awake for at most 16 minutes. Permissions: `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MICROPHONE`, `POST_NOTIFICATIONS` (asked once, from the person's own tap on Talk; the call still works without it), `WAKE_LOCK`. The page tells the app about the call through `IipNative.callStarted()`, `callEnded()` and `callMuted(boolean)`; the notification buttons come back to the page as the `iip:native-mute` and `iip:native-hangup` events.
- Device lock: the app reads `KeyguardManager.isDeviceLocked()` (not the keyguard-showing flag, which some makers report wrongly) and sends `iip:device-lock` to the page whenever it changes, and `IipNative.isLocked()` answers directly. The page then asks the server to withhold figures.
- Every `IipNative` method does nothing unless the WebView is showing IIP's own site, so a link to another website cannot start the service or tap the phone.
- `versionCode` 3 (1.2.0) added the call service and lock state; `versionCode` 4 (1.3.0) adds Hey Kato, closed-app nudges and the device credential; `versionCode` 5 (1.3.1) puts the WebView in a padded `FrameLayout` (a WebView ignores its own padding, so the page used to draw under the status bar). An APK was built with `./build.sh` (below); **none of it has run on a phone**, and Gradle could not start in the authoring environment, so the Gradle build of this version is unverified. Raise `versionCode` again for the next APK.
- Back goes back in the page history.
- **"Hey Kato" (off until switched on in Settings, This phone).** `WakeWordService` is a foreground microphone service running on-device keyword spotting (sherpa-onnx, small English model, fetched by `build.sh`). On a hit it opens IIP, over the lock screen if needed, and the server checks the clip it heard (`POST /api/v1/voice/device/wake`); a "no" closes the conversation again. It steps aside while a call or the page's own microphone is in use. Wake audio is not stored. `IipNative.setWake(boolean)`, `wakeOn()`, `wakeState()`, `takeTalk()`; the page hears `iip:native-talk` and `iip:native-settings`. The thresholds in `wake/keywords.txt` are untuned on real voices.
- **Nudges while the app is closed (on by default, switchable).** `NudgeReceiver` wakes about every 40 minutes (and after a restart), outside weekday 08:00 to 18:00 Kampala time it does nothing, otherwise it asks `GET /api/v1/voice/device/nudge` and shows a notification with Talk now. A locked phone is told no names or figures. Android may delay the check to save battery.
- **The device credential.** The signed-in page calls `IipNative.saveDeviceCheck(token, apiBase)`; the app keeps it in private storage (`allowBackup=false`, https only) and uses it only for those two calls. `hasDeviceCheck()` and `clearDeviceCheck()` (which returns the old value so the page can revoke it on sign-out). It is not the sign-in token and cannot read claims.
- New permissions: `USE_FULL_SCREEN_INTENT` (Android 14 and later needs the person to allow it) and `RECEIVE_BOOT_COMPLETED`; the microphone is asked for only when Hey Kato is switched on. Nothing is granted through adb.

## Known limits

- **Google sign-in does not work in the app.** Google refuses OAuth inside an embedded WebView. Sign in with a Microsoft work account or the email code. Supporting Google needs Chrome Custom Tabs, which is an AndroidX dependency and a change to the "no dependencies" rule.
- **Files the page builds in the browser cannot be downloaded.** The Kato workbook export is built in the page (a `blob:` link), and the system downloader cannot fetch it, so the app says so. Fixing it needs a small native bridge, or a server route that returns the file.
- The portal is built for desktop first. Screens marked desktop-only in the portal stay desktop-only here.
