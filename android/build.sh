#!/usr/bin/env bash
# Builds the IIP Android app with the plain Android SDK build tools (no Gradle), the way the sibling MIP app is built.
#
#   ./build.sh                      -> build/iip.apk
#
# Needs: JDK 17 or newer (Android Studio's bundled JBR is fine), the Android SDK with build-tools 35 and platform
# android-35, Python 3, curl, tar and unzip. Set JAVA_HOME and ANDROID_HOME if they are not already set.
# The first run downloads the "Hey Kato" keyword model (about 14 MB) and the sherpa-onnx library (about 40 MB) into
# build/wake-cache, checking every file against a pinned hash.
# Signing: the SDK's debug key (~/.android/debug.keystore), which is what the Gradle release build uses too, so the
# result installs over a build made that way. It is for sideloading, not for a store.
set -euo pipefail
cd "$(dirname "$0")"

SDK=${ANDROID_HOME:-${ANDROID_SDK_ROOT:-${LOCALAPPDATA:+$LOCALAPPDATA/Android/Sdk}}}
[ -d "${SDK:-}" ] || { echo "Set ANDROID_HOME to the Android SDK folder."; exit 1; }
BT=$(ls -d "$SDK"/build-tools/35.* | sort -V | tail -1)
JAR=$SDK/platforms/android-35/android.jar
[ -f "$JAR" ] || { echo "Missing $JAR (install platform android-35)."; exit 1; }
if [ -n "${JAVA_HOME:-}" ]; then JBIN=$JAVA_HOME/bin; else JBIN=$(dirname "$(command -v javac)"); fi
case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) SEP=';'; win() { cygpath -m "$1"; } ;; *) SEP=':'; win() { printf '%s' "$1"; } ;; esac
EXE=; [ "$SEP" = ';' ] && EXE=.exe

OUT=build
CACHE=$OUT/wake-cache
rm -rf "$OUT/res" "$OUT/gen" "$OUT/classes" "$OUT/dex" "$OUT/assets" "$OUT/lib" "$OUT"/*.apk
mkdir -p "$OUT/res" "$OUT/gen/com/otic/iip" "$OUT/classes" "$OUT/dex" "$OUT/assets/wake" "$OUT/lib" "$CACHE"

# ---- "Hey Kato": on-device keyword spotting with sherpa-onnx (Apache-2.0) and its small English model ----------------
fetch() { # url file sha256
  [ -f "$CACHE/$2" ] || curl -sfL -o "$CACHE/$2" "$1"
  echo "$3  $CACHE/$2" | sha256sum -c --quiet - || { rm -f "$CACHE/$2"; echo "bad download: $2"; exit 1; }
}
fetch https://github.com/k2-fsa/sherpa-onnx/releases/download/v2.0.3/sherpa-onnx-static-link-onnxruntime-2.0.3.aar sherpa.aar \
  7744a3a8f08f017d59c2d8ed8d9b700345561f5abe9f4e06dc6eaef3a0b977b4
fetch https://repo1.maven.org/maven2/org/jetbrains/kotlin/kotlin-stdlib/2.1.0/kotlin-stdlib-2.1.0.jar kotlin-stdlib.jar \
  d6f91b7b0f306cca299fec74fb7c34e4874d6f5ec5b925a0b4de21901e119c3f
KWS=sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01
if [ ! -f "$CACHE/$KWS/tokens.txt" ]; then
  curl -sfL https://github.com/k2-fsa/sherpa-onnx/releases/download/kws-models/$KWS.tar.bz2 | tar -xj -C "$CACHE"
fi
M=$CACHE/$KWS; P=epoch-12-avg-2-chunk-16-left-64.int8.onnx
sha256sum -c --quiet - <<SUMS
1e721676515bcd42a186979733981213c66c80db680e1cc582dfedf3be76e678  $M/encoder-$P
e40ff43297abe815e8898494c17e71bba2152d9d40fa3eb803f75d0f7533329a  $M/decoder-$P
eae9da0c7e1e6c6a3f4cc42d167899c388f6c6701b94cb96320e4f55df79624c  $M/joiner-$P
fd2ded4050a55d2b1578870ba8697d02371980217806b7558bd0a5cc60f3ba53  $M/tokens.txt
SUMS
cp "$M/encoder-$P" "$OUT/assets/wake/encoder.onnx"
cp "$M/decoder-$P" "$OUT/assets/wake/decoder.onnx"
cp "$M/joiner-$P" "$OUT/assets/wake/joiner.onnx"
cp "$M/tokens.txt" wake/keywords.txt "$OUT/assets/wake/"
if [ ! -f "$CACHE/sherpa.jar" ]; then
  ( cd "$CACHE" && rm -rf aar && mkdir aar && cd aar && unzip -q -o ../sherpa.aar classes.jar 'jni/armeabi-v7a/*' 'jni/arm64-v8a/*' \
    && cp classes.jar ../sherpa.jar && rm -rf ../jni && mv jni ../jni )
fi
cp -r "$CACHE/jni/." "$OUT/lib/"
LIBS="$(win "$CACHE/sherpa.jar")$SEP$(win "$CACHE/kotlin-stdlib.jar")"

# ---- the class Gradle would generate: the start address lives in app/build.gradle, in one place -----------------------
START_URL=$(grep -o 'START_URL", *"\\"[^"\\]*' app/build.gradle | sed 's/.*\\"//')
[ -n "$START_URL" ] || { echo "START_URL not found in app/build.gradle"; exit 1; }
cat > "$OUT/gen/com/otic/iip/BuildConfig.java" <<EOF
package com.otic.iip;

public final class BuildConfig {
    public static final boolean DEBUG = false;
    public static final String START_URL = "$START_URL";
    private BuildConfig() {}
}
EOF
CODE=$(grep -o 'versionCode [0-9]*' app/build.gradle | grep -o '[0-9]*')
NAME=$(grep -o 'versionName "[^"]*"' app/build.gradle | sed 's/versionName "//; s/"//')

# ---- resources, code, dex, package, sign ---------------------------------------------------------------------------
"$BT/aapt2$EXE" compile --dir "$(win app/src/main/res)" -o "$(win "$OUT/res/res.zip")"
# Gradle takes the package from `namespace`; aapt2 wants it in the manifest, so a copy with it added is used here.
PACKAGE=$(grep -o 'namespace "[^"]*"' app/build.gradle | sed 's/namespace "//; s/"//')
sed "s|<manifest |<manifest package=\"$PACKAGE\" |" app/src/main/AndroidManifest.xml > "$OUT/AndroidManifest.xml"
"$BT/aapt2$EXE" link -o "$(win "$OUT/base.apk")" -I "$(win "$JAR")" --manifest "$(win "$OUT/AndroidManifest.xml")" \
  --min-sdk-version 26 --target-sdk-version 35 --version-code "$CODE" --version-name "$NAME" \
  --auto-add-overlay -A "$(win "$OUT/assets")" "$(win "$OUT/res/res.zip")"
SOURCES=$(find app/src/main/java "$OUT/gen" -name '*.java' | while read -r f; do win "$f"; done)
"$JBIN/javac$EXE" -source 17 -target 17 -nowarn -Xlint:-options -encoding UTF-8 -classpath "$(win "$JAR")$SEP$LIBS" -d "$(win "$OUT/classes")" $SOURCES
CLASSES=$(find "$OUT/classes" -name '*.class' | while read -r f; do win "$f"; done)
"$JBIN/java$EXE" -cp "$(win "$BT/lib/d8.jar")" com.android.tools.r8.D8 --release --min-api 26 --lib "$(win "$JAR")" \
  --output "$(win "$OUT/dex")" $CLASSES "$(win "$CACHE/sherpa.jar")" "$(win "$CACHE/kotlin-stdlib.jar")" >"$OUT/d8.log" 2>&1 || { cat "$OUT/d8.log"; exit 1; }
grep -v -e "kotlin.Metadata" -e "parsing kotlin metadata" "$OUT/d8.log" || true
python "$(dirname "$0")/pack.py" "$OUT/base.apk" "$OUT/dex" "$OUT/lib"
"$BT/zipalign$EXE" -f -p 4 "$(win "$OUT/base.apk")" "$(win "$OUT/aligned.apk")"

KS=${IIP_KEYSTORE:-$HOME/.android/debug.keystore}
KALIAS=${IIP_KEY_ALIAS:-androiddebugkey}
KPASS=${IIP_KEY_PASS:-android}
[ -f "$KS" ] || { echo "No keystore at $KS (set IIP_KEYSTORE, IIP_KEY_ALIAS and IIP_KEY_PASS)."; exit 1; }
"$JBIN/java$EXE" -jar "$(win "$BT/lib/apksigner.jar")" sign --ks "$(win "$KS")" --ks-key-alias "$KALIAS" --ks-pass "pass:$KPASS" \
  --out "$(win "$OUT/iip.apk")" "$(win "$OUT/aligned.apk")"
"$JBIN/java$EXE" -jar "$(win "$BT/lib/apksigner.jar")" verify "$(win "$OUT/iip.apk")"
echo "built $(pwd)/$OUT/iip.apk (version $NAME, code $CODE, $(du -h "$OUT/iip.apk" | cut -f1))"
