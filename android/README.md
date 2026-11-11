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

The APK is `app/build/outputs/apk/release/app-release.apk`. Copy it to the repo root as `IIP.apk` to sideload it (not committed). The release build is signed with the debug key so it installs without a keystore, which is fine for sideloading and not for the Play Store. Raise `versionCode` for every APK handed to someone who already has the app installed. Confirm a build with `aapt dump badging IIP.apk`.

## What the shell does

- Keeps `http` and `https` links inside the app and sends every other scheme to the phone.
- Accepts cookies, so the Microsoft sign-in session survives. The portal signs in by redirect, which works in a WebView.
- File upload through the system picker; downloads through the system downloader, with the session cookie.
- Pads the page by the status bar, navigation bar and keyboard, because an app targeting SDK 35 otherwise draws underneath them.
- Back goes back in the page history.

## Known limits

- **Google sign-in does not work in the app.** Google refuses OAuth inside an embedded WebView. Sign in with a Microsoft work account or the email code. Supporting Google needs Chrome Custom Tabs, which is an AndroidX dependency and a change to the "no dependencies" rule.
- **Files the page builds in the browser cannot be downloaded.** The Kato workbook export is built in the page (a `blob:` link), and the system downloader cannot fetch it, so the app says so. Fixing it needs a small native bridge, or a server route that returns the file.
- The portal is built for desktop first. Screens marked desktop-only in the portal stay desktop-only here.
