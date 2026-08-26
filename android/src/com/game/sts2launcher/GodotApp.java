package com.game.sts2launcher;

import org.godotengine.godot.Godot;
import org.godotengine.godot.GodotActivity;

import android.content.Intent;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.Bundle;
import android.os.PowerManager;
import android.util.Log;

import androidx.activity.EdgeToEdge;
import androidx.browser.customtabs.CustomTabsIntent;
import androidx.core.content.FileProvider;
import androidx.core.splashscreen.SplashScreen;

import android.net.Uri;

import android.content.SharedPreferences;

import java.net.URLEncoder;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.security.KeyStore;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

import android.content.Context;
import android.net.wifi.WifiManager;
import android.util.Base64;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import org.fmod.FMOD;

// Main activity for the mobile launcher. Handles FMOD initialization, .NET assembly
// setup, PCK loading, LAN multicast, and Android Keystore encryption for credentials.
public class GodotApp extends GodotActivity {
	static {
		// FMOD must load before Godot's GDExtension or FMOD_JNI_GetEnv fails.
		System.loadLibrary("fmod");
		System.loadLibrary("fmodstudio");
		// Required for TLS/SSL (SteamKit2 WebSocket, HTTPS).
		System.loadLibrary("System.Security.Cryptography.Native.Android");
	}

	private static GodotApp instance;
	private WifiManager.MulticastLock multicastLock;
	private String gameDir;
	// WebView shown over the Godot SurfaceView for in-app article viewing
	// (Steam community announcements, etc.). Null when no overlay is active.
	private FrameLayout webViewOverlay;
	private WebView activeWebView;
	// Flipped true once Godot has produced its first frame so the Android splash can
	// finally dismiss. Without this the system splash disappears as soon as the Activity
	// view becomes visible, exposing several seconds of black Godot SurfaceView while
	// Mono + Harmony + game init runs.
	private final AtomicBoolean godotReady = new AtomicBoolean(false);
	private static final String TAG = "STS2Mobile";
	private static final String KEYSTORE_ALIAS = "sts2mobile_credentials";
	private static final String PCK_FILE = "SlayTheSpire2.pck";

	private final Runnable updateWindowAppearance = () -> {
		Godot godot = getGodot();
		if (godot != null) {
			godot.enableImmersiveMode(true, true);
			godot.enableEdgeToEdge(true, true);
			godot.setSystemBarsAppearance();
		}
	};

	@Override
	public void onCreate(Bundle savedInstanceState) {
		instance = this;
		gameDir = new File(getFilesDir(), "game").getAbsolutePath();

		SplashScreen splash = SplashScreen.installSplashScreen(this);
		// Dismiss as soon as Android is ready: the activity's windowBackground is
		// the key art, so releasing the icon splash early shows artwork during
		// Godot's boot rather than a static icon.
		splash.setKeepOnScreenCondition(() -> false);
		EdgeToEdge.enable(this);

		// Must be called before any native FMOD calls.
		FMOD.init(this);

		versionChanged = isNewVersion();

		setupAssemblies();
		extractAssetFile("FMOD_LOGOS/FMOD Logo White - Transparent Background.png", "fmod_logo.png");
		extractAssetFile("launcher_bg.png", "launcher_bg.png");
		extractAssetFile("launcher_font.ttf", "launcher_font.ttf");
		extractAssetFile("launcher_logo.png", "launcher_logo.png");

		super.onCreate(savedInstanceState);

		// Android WiFi power saving drops broadcast packets without a MulticastLock.
		try {
			WifiManager wifiMgr = (WifiManager) getApplicationContext().getSystemService(Context.WIFI_SERVICE);
			multicastLock = wifiMgr.createMulticastLock("sts2_lan_discovery");
			multicastLock.setReferenceCounted(false);
			multicastLock.acquire();
			Log.i(TAG, "WiFi MulticastLock acquired for LAN discovery");
		} catch (Exception e) {
			Log.w(TAG, "Failed to acquire MulticastLock", e);
		}
	}

	// Whether this launch is the first on a newly installed APK. Read once and
	// cached: isNewVersion() records the new code, so a second call returns false.
	private boolean versionChanged;

	private boolean isNewVersion() {
		SharedPreferences prefs = getSharedPreferences("sts2mobile", MODE_PRIVATE);
		int lastVersion = prefs.getInt("installed_version_code", -1);
		int currentVersion = BuildConfig.VERSION_CODE;
		if (lastVersion == currentVersion) {
			return false;
		}
		Log.i(TAG, "Version changed: " + lastVersion + " -> " + currentVersion);
		prefs.edit().putInt("installed_version_code", currentVersion).apply();
		return true;
	}

	// Copies .NET BCL from APK assets and game assemblies from the download
	// directory
	// into the location Godot expects. Skips if already done unless the APK version
	// changed.
	private void setupAssemblies() {
		File srcDir = findAssembliesDir();
		File destDir = new File(getFilesDir(), ".godot/mono/publish/arm64");

		File patcherMarker = new File(destDir, "STS2Mobile.dll");
		File sts2Marker = new File(destDir, "sts2.dll");
		if (sts2Marker.exists() && patcherMarker.exists() && !versionChanged) {
			Log.i(TAG, "Assemblies already set up at: " + destDir.getAbsolutePath());
			return;
		}

		if (versionChanged) {
			Log.i(TAG, "New version detected, re-copying all assemblies");
		}

		destDir.mkdirs();

		try {
			String[] bclFiles = getAssets().list("dotnet_bcl");
			if (bclFiles != null) {
				int count = 0;
				for (String name : bclFiles) {
					try (InputStream in = getAssets().open("dotnet_bcl/" + name);
							OutputStream out = new FileOutputStream(new File(destDir, name))) {
						byte[] buf = new byte[8192];
						int len;
						while ((len = in.read(buf)) > 0) {
							out.write(buf, 0, len);
						}
						count++;
					}
				}
				Log.i(TAG, "Copied " + count + " BCL assemblies from assets");
			}
		} catch (IOException e) {
			Log.e(TAG, "Failed to copy BCL assemblies", e);
		}

		// Only copy game assemblies that don't already exist in BCL. The depot has
		// desktop
		// CoreCLR versions that are incompatible with Android's Mono runtime.
		if (!srcDir.exists() || !srcDir.isDirectory()) {
			Log.w(TAG, "Game assemblies source dir not found: " + srcDir.getAbsolutePath());
			return;
		}

		Log.i(TAG, "Copying game assemblies from " + srcDir + " to " + destDir);
		File[] files = srcDir.listFiles();
		if (files == null)
			return;

		int count = 0;
		for (File src : files) {
			if (src.isFile()) {
				String name = src.getName();
				if (name.endsWith(".so")) {
					continue;
				}
				File dest = new File(destDir, name);
				if (dest.exists()) {
					continue;
				}
				try {
					copyFile(src, dest);
					count++;
				} catch (IOException e) {
					Log.e(TAG, "Failed to copy: " + name, e);
				}
			}
		}
		Log.i(TAG, "Copied " + count + " game assembly files");
	}

	private File findAssembliesDir() {
		File gameDirFile = new File(gameDir);
		if (gameDirFile.exists() && gameDirFile.isDirectory()) {
			File[] children = gameDirFile.listFiles();
			if (children != null) {
				for (File child : children) {
					if (child.isDirectory() && child.getName().startsWith("data_")) {
						Log.i(TAG, "Found assemblies dir: " + child.getName());
						return child;
					}
				}
			}
		}
		return new File(gameDir, "data_sts2_windows_x86_64");
	}

	private void copyFile(File src, File dest) throws IOException {
		try (InputStream in = new FileInputStream(src);
				OutputStream out = new FileOutputStream(dest)) {
			byte[] buf = new byte[8192];
			int len;
			while ((len = in.read(buf)) > 0) {
				out.write(buf, 0, len);
			}
		}
	}

	// Extracts a single file from APK assets to the files directory.
	private void extractAssetFile(String assetPath, String destName) {
		File dest = new File(getFilesDir(), destName);
		if (dest.exists() && !versionChanged)
			return;
		try (InputStream in = getAssets().open(assetPath);
				OutputStream out = new FileOutputStream(dest)) {
			byte[] buf = new byte[4096];
			int len;
			while ((len = in.read(buf)) > 0) {
				out.write(buf, 0, len);
			}
		} catch (IOException e) {
			Log.w(TAG, "Failed to extract " + assetPath, e);
		}
	}

	@Override
	public List<String> getCommandLine() {
		List<String> commands = new ArrayList<>(super.getCommandLine());
		File pckFile = new File(gameDir, PCK_FILE);
		if (pckFile.exists()) {
			commands.add("--main-pack");
			commands.add(pckFile.getAbsolutePath());
			Log.i(TAG, "Loading PCK from: " + pckFile.getAbsolutePath());
		} else {
			// No game files yet; use bootstrap PCK so Godot can initialize for the
			// launcher.
			String bootstrapPck = extractBootstrapPck();
			if (bootstrapPck != null) {
				commands.add("--main-pack");
				commands.add(bootstrapPck);
				Log.i(TAG, "Using bootstrap PCK for launcher-only mode");
			}
		}
		return commands;
	}

	private String extractBootstrapPck() {
		File dest = new File(getFilesDir(), "bootstrap.pck");
		if (dest.exists()) {
			return dest.getAbsolutePath();
		}
		try (InputStream in = getAssets().open("bootstrap.pck");
				OutputStream out = new FileOutputStream(dest)) {
			byte[] buf = new byte[4096];
			int len;
			while ((len = in.read(buf)) > 0) {
				out.write(buf, 0, len);
			}
			return dest.getAbsolutePath();
		} catch (IOException e) {
			Log.e(TAG, "Failed to extract bootstrap PCK", e);
			return null;
		}
	}

	@Override
	public void onResume() {
		super.onResume();
		updateWindowAppearance.run();
	}

	@Override
	public void onGodotMainLoopStarted() {
		super.onGodotMainLoopStarted();
		// Allow the Android system splash to dismiss now that Godot is actually
		// rendering; before this point the window is a black SurfaceView.
		godotReady.set(true);
		runOnUiThread(updateWindowAppearance);
	}

	@Override
	protected void onDestroy() {
		if (multicastLock != null && multicastLock.isHeld()) {
			multicastLock.release();
			Log.i(TAG, "WiFi MulticastLock released");
		}
		FMOD.close();
		super.onDestroy();
	}

	public static GodotApp getInstance() {
		return instance;
	}

	public String getGameDir() {
		return gameDir;
	}

	public String getVersionName() {
		return BuildConfig.VERSION_NAME;
	}

	// Thermal throttling level reported by the platform, for the debug overlay.
	// PowerManager.getCurrentThermalStatus() is available to normal apps from
	// API 29; returns an empty string when unavailable so callers can hide the field.
	public String getThermalStatus() {
		try {
			if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
				return "";
			}
			PowerManager pm = (PowerManager) getSystemService(POWER_SERVICE);
			if (pm == null) {
				return "";
			}
			switch (pm.getCurrentThermalStatus()) {
				case PowerManager.THERMAL_STATUS_NONE:      return "NONE";
				case PowerManager.THERMAL_STATUS_LIGHT:     return "LIGHT";
				case PowerManager.THERMAL_STATUS_MODERATE:  return "MODERATE";
				case PowerManager.THERMAL_STATUS_SEVERE:    return "SEVERE";
				case PowerManager.THERMAL_STATUS_CRITICAL:  return "CRITICAL";
				case PowerManager.THERMAL_STATUS_EMERGENCY: return "EMERGENCY";
				case PowerManager.THERMAL_STATUS_SHUTDOWN:  return "SHUTDOWN";
				default:                                    return "";
			}
		} catch (Exception e) {
			Log.w(TAG, "getThermalStatus failed", e);
			return "";
		}
	}

	// Ends the app rather than bouncing back to the launcher. finishAndRemoveTask
	// drops the task from recents so this reads as a deliberate exit, and the
	// explicit exit follows because Godot's process does not reliably unwind on
	// its own once the activity is gone.
	public void quitApp() {
		Log.i(TAG, "Quit requested, exiting app");
		finishAndRemoveTask();

		// finishAndRemoveTask posts the teardown to the main looper, so exiting on
		// the next line would kill the process before onDestroy ran. Queueing the
		// exit behind it lets the lifecycle finish first. The exit is still needed:
		// Godot's process does not reliably unwind once the activity is gone.
		new Handler(Looper.getMainLooper()).post(() -> Runtime.getRuntime().exit(0));
	}

	public void restartApp() {
		Log.i(TAG, "Restarting app...");
		Intent intent = getPackageManager().getLaunchIntentForPackage(getPackageName());
		if (intent != null) {
			intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
			startActivity(intent);
		}
		Runtime.getRuntime().exit(0);
	}

	// Returns the absolute directory where the C# updater should drop the
	// downloaded APK before calling installApk. Matches res/xml/file_paths.xml
	// so FileProvider can expose the URI to PackageInstaller.
	public String getUpdatesDir() {
		File dir = new File(getFilesDir(), "updates");
		if (!dir.exists()) {
			dir.mkdirs();
		}
		return dir.getAbsolutePath();
	}

	// True iff this app is currently allowed to kick off a package install.
	// Android 8+ gates REQUEST_INSTALL_PACKAGES behind a per-source toggle
	// in system settings; older OS versions implicitly allow it once the
	// manifest permission is declared.
	public boolean canInstallPackages() {
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
			return getPackageManager().canRequestPackageInstalls();
		}
		return true;
	}

	// Opens the system settings page where the user grants this app the
	// "install unknown apps" permission. No-op on < Android 8.
	public void requestInstallPackagesPermission() {
		if (android.os.Build.VERSION.SDK_INT < android.os.Build.VERSION_CODES.O) {
			return;
		}
		try {
			Intent intent = new Intent(android.provider.Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES)
					.setData(Uri.parse("package:" + getPackageName()))
					.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			startActivity(intent);
		} catch (Exception e) {
			Log.w(TAG, "Failed to open unknown-sources settings", e);
		}
	}

	// Hands the APK to PackageInstaller via FileProvider. Android shows a
	// confirmation dialog before actually replacing the running app — we
	// cannot bypass that, it's system-level.
	public boolean installApk(String apkPath) {
		try {
			File apk = new File(apkPath);
			if (!apk.exists() || apk.length() == 0) {
				Log.e(TAG, "installApk: file missing or empty at " + apkPath);
				return false;
			}

			Uri uri = FileProvider.getUriForFile(
					this, getPackageName() + ".fileprovider", apk);

			Intent intent = new Intent(Intent.ACTION_VIEW)
					.setDataAndType(uri, "application/vnd.android.package-archive")
					.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK
							| Intent.FLAG_GRANT_READ_URI_PERMISSION);
			startActivity(intent);
			Log.i(TAG, "installApk: launched PackageInstaller for " + apkPath);
			return true;
		} catch (Exception e) {
			Log.e(TAG, "installApk failed for " + apkPath, e);
			return false;
		}
	}

	// AES-256-GCM encryption via Android Keystore (hardware-backed TEE).
	private SecretKey getOrCreateKeystoreKey() throws Exception {
		KeyStore keyStore = KeyStore.getInstance("AndroidKeyStore");
		keyStore.load(null);

		if (keyStore.containsAlias(KEYSTORE_ALIAS)) {
			return ((KeyStore.SecretKeyEntry) keyStore.getEntry(KEYSTORE_ALIAS, null)).getSecretKey();
		}

		KeyGenerator keyGen = KeyGenerator.getInstance(
				android.security.keystore.KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
		keyGen.init(new android.security.keystore.KeyGenParameterSpec.Builder(
				KEYSTORE_ALIAS,
				android.security.keystore.KeyProperties.PURPOSE_ENCRYPT
						| android.security.keystore.KeyProperties.PURPOSE_DECRYPT)
				.setBlockModes(android.security.keystore.KeyProperties.BLOCK_MODE_GCM)
				.setEncryptionPaddings(android.security.keystore.KeyProperties.ENCRYPTION_PADDING_NONE)
				.setKeySize(256)
				.build());
		return keyGen.generateKey();
	}

	public String encryptString(String plaintext) {
		try {
			SecretKey key = getOrCreateKeystoreKey();
			Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
			cipher.init(Cipher.ENCRYPT_MODE, key);
			byte[] iv = cipher.getIV();
			byte[] ciphertext = cipher.doFinal(plaintext.getBytes("UTF-8"));

			// Format: [iv_length (1 byte)] [iv] [ciphertext]
			byte[] result = new byte[1 + iv.length + ciphertext.length];
			result[0] = (byte) iv.length;
			System.arraycopy(iv, 0, result, 1, iv.length);
			System.arraycopy(ciphertext, 0, result, 1 + iv.length, ciphertext.length);
			return Base64.encodeToString(result, Base64.NO_WRAP);
		} catch (Exception e) {
			Log.e(TAG, "Encryption failed", e);
			return null;
		}
	}

	public String decryptString(String encrypted) {
		try {
			byte[] blob = Base64.decode(encrypted, Base64.NO_WRAP);
			int ivLength = blob[0] & 0xFF;
			byte[] iv = new byte[ivLength];
			System.arraycopy(blob, 1, iv, 0, ivLength);
			byte[] ciphertext = new byte[blob.length - 1 - ivLength];
			System.arraycopy(blob, 1 + ivLength, ciphertext, 0, ciphertext.length);

			SecretKey key = getOrCreateKeystoreKey();
			Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
			cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(128, iv));
			byte[] plaintext = cipher.doFinal(ciphertext);
			return new String(plaintext, "UTF-8");
		} catch (Exception e) {
			Log.e(TAG, "Decryption failed", e);
			return null;
		}
	}

	// Returns true if the app has permission to write to shared external storage.
	public boolean hasStoragePermission() {
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
			return android.os.Environment.isExternalStorageManager();
		}
		return checkSelfPermission(
				android.Manifest.permission.WRITE_EXTERNAL_STORAGE) == android.content.pm.PackageManager.PERMISSION_GRANTED;
	}

	// Requests external storage permission. On Android 11+, opens the system
	// settings
	// page for "All files access". On older versions, shows the runtime permission
	// dialog.
	public void requestStoragePermission() {
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
			try {
				Intent intent = new Intent(android.provider.Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION);
				intent.setData(android.net.Uri.parse("package:" + getPackageName()));
				startActivity(intent);
			} catch (Exception e) {
				Log.w(TAG, "Failed to open app-specific storage settings, trying general", e);
				Intent intent = new Intent(android.provider.Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION);
				startActivity(intent);
			}
		} else {
			requestPermissions(new String[] { android.Manifest.permission.WRITE_EXTERNAL_STORAGE }, 1);
		}
	}

	// Opens the given URL in a WebView overlay on top of the Godot view.
	// Used for in-app viewing of Steam community announcements so the user
	// doesn't lose the launcher state by getting kicked into a browser app.
	// Idempotent: closes any existing overlay before opening a new one.
	public void showWebView(String url) {
		if (url == null || url.isEmpty()) {
			return;
		}

		// Preferred path: a Custom Tab. It renders as part of the launcher rather
		// than sending the user off to a browser app, and being a real browser it
		// brings its own page translation — which is the only translation route
		// still standing. Every URL-based translation proxy this used to rely on
		// is gone: Microsoft retired translatetheweb.com, Google's URL translation
		// is blocked in Korea along with the known workaround, and Papago has no
		// deep link (its /website endpoint drops the parameters and redirects to
		// the home page).
		if (openInCustomTab(url)) {
			return;
		}

		runOnUiThread(() -> {
			closeWebViewInternal();

			float density = getResources().getDisplayMetrics().density;
			int barHeight = (int) (52 * density);

			FrameLayout overlay = new FrameLayout(this);
			overlay.setBackgroundColor(0xFF111114);
			overlay.setClickable(true);

			// Top bar: close button + URL label, anchored above the WebView.
			LinearLayout topBar = new LinearLayout(this);
			topBar.setOrientation(LinearLayout.HORIZONTAL);
			topBar.setBackgroundColor(0xFF1F2024);
			topBar.setGravity(Gravity.CENTER_VERTICAL);
			topBar.setPadding(
					(int) (12 * density), 0, (int) (12 * density), 0);

			Button closeButton = new Button(this);
			closeButton.setText("✕  Close");
			closeButton.setAllCaps(false);
			closeButton.setTextColor(0xFFE6E6EA);
			closeButton.setBackgroundColor(0xFF2D2F35);
			LinearLayout.LayoutParams closeParams = new LinearLayout.LayoutParams(
					ViewGroup.LayoutParams.WRAP_CONTENT,
					(int) (40 * density));
			closeParams.rightMargin = (int) (12 * density);
			topBar.addView(closeButton, closeParams);

			// Korean site-translation toggle. We use Microsoft's
			// translatetheweb.com — it's reachable from Korea (unlike
			// Google's `.translate.goog`) and accepts a URL parameter
			// for whole-page translation (unlike Papago, whose `st=`
			// is text-only and lands on the home page). Steam's
			// `?l=koreana` only swaps Steam's own chrome strings; it
			// won't translate community announcement bodies, which is
			// what the user actually wants to read.
			Button translateButton = new Button(this);
			translateButton.setText("🌐 KO");
			translateButton.setAllCaps(false);
			translateButton.setTextColor(0xFFE6E6EA);
			translateButton.setBackgroundColor(0xFF2D2F35);
			LinearLayout.LayoutParams translateParams = new LinearLayout.LayoutParams(
					ViewGroup.LayoutParams.WRAP_CONTENT,
					(int) (40 * density));
			translateParams.rightMargin = (int) (12 * density);
			topBar.addView(translateButton, translateParams);

			TextView urlLabel = new TextView(this);
			urlLabel.setText(url);
			urlLabel.setTextColor(0xFFB0B0BA);
			urlLabel.setSingleLine(true);
			urlLabel.setEllipsize(android.text.TextUtils.TruncateAt.END);
			urlLabel.setTextSize(11);
			topBar.addView(urlLabel, new LinearLayout.LayoutParams(
					0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

			FrameLayout.LayoutParams topBarParams = new FrameLayout.LayoutParams(
					ViewGroup.LayoutParams.MATCH_PARENT, barHeight,
					Gravity.TOP);
			overlay.addView(topBar, topBarParams);

			// Indeterminate progress strip just under the bar while pages load.
			ProgressBar progress = new ProgressBar(
					this, null, android.R.attr.progressBarStyleHorizontal);
			progress.setIndeterminate(true);
			FrameLayout.LayoutParams progressParams = new FrameLayout.LayoutParams(
					ViewGroup.LayoutParams.MATCH_PARENT,
					(int) (3 * density),
					Gravity.TOP);
			progressParams.topMargin = barHeight;
			overlay.addView(progress, progressParams);

			WebView webView = new WebView(this);
			WebSettings settings = webView.getSettings();
			settings.setJavaScriptEnabled(true);
			settings.setDomStorageEnabled(true);
			settings.setLoadWithOverviewMode(true);
			settings.setUseWideViewPort(true);
			settings.setBuiltInZoomControls(true);
			settings.setDisplayZoomControls(false);
			settings.setMixedContentMode(
					WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
			webView.setWebViewClient(new WebViewClient() {
				@Override
				public void onPageFinished(WebView view, String finishedUrl) {
					progress.setVisibility(View.GONE);
					urlLabel.setText(finishedUrl);
				}

				@Override
				public void onPageStarted(
						WebView view, String startedUrl,
						android.graphics.Bitmap favicon) {
					progress.setVisibility(View.VISIBLE);
				}

				@Override
				public void onReceivedError(
						WebView view, int errorCode,
						String description, String failingUrl) {
					progress.setVisibility(View.GONE);
				}
			});
			webView.setWebChromeClient(new WebChromeClient());
			FrameLayout.LayoutParams webParams = new FrameLayout.LayoutParams(
					ViewGroup.LayoutParams.MATCH_PARENT,
					ViewGroup.LayoutParams.MATCH_PARENT,
					Gravity.TOP);
			webParams.topMargin = barHeight;
			overlay.addView(webView, webParams);

			final String originalUrl = url;
			final boolean[] translateActive = { false };
			Runnable refreshTranslateButton = () -> translateButton.setText(
					translateActive[0] ? "🌐 KO ✓" : "🌐 KO");

			webView.loadUrl(maybeAddKoreanLocale(originalUrl));

			translateButton.setOnClickListener(v -> {
				translateActive[0] = !translateActive[0];
				refreshTranslateButton.run();
				// Always navigate from the original (un-localised) URL so the
				// proxy isn't given a `?l=koreana`-decorated URL it would then
				// re-translate. The proxy will convert the announcement body
				// to Korean directly.
				if (translateActive[0]) {
					webView.loadUrl(buildSiteTranslatorUrl(originalUrl));
				} else {
					webView.loadUrl(maybeAddKoreanLocale(originalUrl));
				}
			});

			// Capture system back gestures while the overlay is up — prefer
			// in-page back navigation, fall back to closing the overlay.
			webView.setFocusableInTouchMode(true);
			webView.requestFocus();
			webView.setOnKeyListener((v, keyCode, event) -> {
				if (event.getAction() == KeyEvent.ACTION_DOWN
						&& keyCode == KeyEvent.KEYCODE_BACK) {
					if (webView.canGoBack()) {
						webView.goBack();
					} else {
						closeWebViewInternal();
					}
					return true;
				}
				return false;
			});

			closeButton.setOnClickListener(v -> closeWebViewInternal());

			FrameLayout root = (FrameLayout) findViewById(android.R.id.content);
			root.addView(overlay, new FrameLayout.LayoutParams(
					ViewGroup.LayoutParams.MATCH_PARENT,
					ViewGroup.LayoutParams.MATCH_PARENT));

			webViewOverlay = overlay;
			activeWebView = webView;
			Log.i(TAG, "showWebView: opened " + url);
		});
	}

	// Returns false when no browser on the device supports Custom Tabs, in which
	// case the caller falls back to the bundled WebView.
	private boolean openInCustomTab(String url) {
		try {
			CustomTabsIntent intent = new CustomTabsIntent.Builder()
					.setShowTitle(true)
					.setUrlBarHidingEnabled(true)
					.build();
			intent.intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			intent.launchUrl(this, Uri.parse(url));
			return true;
		} catch (Exception e) {
			Log.w(TAG, "Custom Tab unavailable, falling back to the in-app WebView", e);
			return false;
		}
	}

	public void closeWebView() {
		runOnUiThread(this::closeWebViewInternal);
	}

	// Adds `l=koreana` to Steam URLs so the Steam-rendered chrome
	// (header, navigation, footer) is in Korean. Note: this does NOT
	// translate community announcement bodies — those are written by
	// the developers and stored verbatim. Use the 🌐 KO toggle
	// (buildSiteTranslatorUrl) for full body translation. For non-Steam
	// hosts we leave the URL alone.
	private static String maybeAddKoreanLocale(String url) {
		if (url == null || url.isEmpty()) {
			return url;
		}
		try {
			Uri u = Uri.parse(url);
			String host = u.getHost();
			if (host == null) {
				return url;
			}
			boolean isSteam =
					host.equals("steamcommunity.com")
							|| host.endsWith(".steamcommunity.com")
							|| host.equals("store.steampowered.com")
							|| host.endsWith(".steampowered.com");
			if (!isSteam) {
				return url;
			}
			// Don't double-add or stomp on an explicit user choice.
			String existing = u.getQueryParameter("l");
			if (existing != null) {
				return url;
			}
			String separator = (u.getQuery() == null || u.getQuery().isEmpty())
					? "?"
					: "&";
			String fragment = u.getFragment();
			String base = fragment == null
					? url
					: url.substring(0, url.length() - (fragment.length() + 1));
			String result = base + separator + "l=koreana";
			if (fragment != null) {
				result += "#" + fragment;
			}
			return result;
		} catch (Exception e) {
			Log.w(TAG, "maybeAddKoreanLocale failed for " + url, e);
			return url;
		}
	}

	// Wraps an arbitrary URL in Microsoft's translatetheweb.com proxy so
	// the page is rendered in Korean. We picked Microsoft over the
	// alternatives because:
	//   - Google's `.translate.goog` returns "This translation service
	//     isn't available in your region" for KR clients.
	//   - Naver Papago has no public bookmarkable URL for site
	//     translation; `?st=URL` is the text endpoint and just lands on
	//     the Papago home page.
	//   - DeepL and Yandex don't expose a stable site-translate URL
	//     either (DeepL is text-only, Yandex's translate.yandex.com
	//     wraps results inconsistently for SPA-heavy pages).
	// translatetheweb.com has been Microsoft's branded site translator
	// for over a decade and serves Korean targets fine from KR networks.
	private static String buildSiteTranslatorUrl(String url) {
		if (url == null || url.isEmpty()) {
			return url;
		}
		try {
			String encoded = URLEncoder.encode(url, "UTF-8");
			return "https://www.translatetheweb.com/?from=&to=ko&a=" + encoded;
		} catch (Exception e) {
			Log.w(TAG, "buildSiteTranslatorUrl failed for " + url, e);
			return url;
		}
	}

	private void closeWebViewInternal() {
		if (webViewOverlay == null) {
			return;
		}
		ViewGroup parent = (ViewGroup) webViewOverlay.getParent();
		if (parent != null) {
			parent.removeView(webViewOverlay);
		}
		if (activeWebView != null) {
			activeWebView.stopLoading();
			activeWebView.loadUrl("about:blank");
			activeWebView.removeAllViews();
			activeWebView.destroy();
		}
		webViewOverlay = null;
		activeWebView = null;
		updateWindowAppearance.run();
	}

	@Override
	public void onBackPressed() {
		// Surface back-press through to the WebView while the overlay is up;
		// the OnKeyListener inside the overlay handles dismissal/back.
		if (activeWebView != null) {
			if (activeWebView.canGoBack()) {
				activeWebView.goBack();
			} else {
				closeWebViewInternal();
			}
			return;
		}
		super.onBackPressed();
	}

	public void deleteKeystoreKey() {
		try {
			KeyStore keyStore = KeyStore.getInstance("AndroidKeyStore");
			keyStore.load(null);
			keyStore.deleteEntry(KEYSTORE_ALIAS);
		} catch (Exception e) {
			Log.e(TAG, "Failed to delete keystore key", e);
		}
	}
}
