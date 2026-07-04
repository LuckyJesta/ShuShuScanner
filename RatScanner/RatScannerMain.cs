using RatEye;
using ShuShuscanner.Scan;
using RatStash;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Drawing.Size;
using Timer = System.Threading.Timer;

namespace ShuShuscanner;

public class ShuShuscannerMain : INotifyPropertyChanged {
	private static ShuShuscannerMain _instance = null!;
	internal static ShuShuscannerMain Instance => _instance ??= new ShuShuscannerMain();

	internal readonly HotkeyManager HotkeyManager;

	private Timer? _marketDBRefreshTimer;
	private Timer? _tarkovTrackerDBRefreshTimer;
	private Timer? _scanRefreshTimer;
	private Timer? _scanStatusClearTimer;
	private long _scanStatusVersion;
	private readonly LocalizationService _localizationService = new();
	private string _scanStatusKey = "";
	private object[] _scanStatusArgs = Array.Empty<object>();

	/// <summary>
	/// Lock for name scanning
	/// </summary>
	/// <remarks>
	/// Lock order: 0
	/// </remarks>
	internal static object NameScanLock = new();

	/// <summary>
	/// Lock for icon scanning
	/// </summary>
	/// <remarks>
	/// Lock order: 1
	/// </remarks>
	internal static object IconScanLock = new();

	public TarkovTrackerDB TarkovTrackerDB;

	internal RatEyeEngine RatEyeEngine;

	public event PropertyChangedEventHandler? PropertyChanged;

	internal ItemQueue ItemScans = new();

	public string ScanStatusText { get; private set; } = "";

	public bool HasScanStatus => !string.IsNullOrWhiteSpace(ScanStatusText);

	public ShuShuscannerMain() {
		_instance = this;

		// Remove old log
		Logger.Clear();

		Logger.LogInfo("----- ShuShuscanner " + RatConfig.Version + " -----");

		Logger.LogInfo($"Screen Info: {RatConfig.ScreenWidth}x{RatConfig.ScreenHeight} at {RatConfig.ScreenScale * 100}%");

		Logger.LogInfo("Initializing TarkovDev API...");
		
		// Try to load from offline cache first for faster startup
		if (TarkovDevAPI.TryInitializeCacheFromOffline()) {
			Logger.LogInfo("Using offline cache for fast startup.");
		} else {
			// No offline cache available, wait for network requests
			Logger.LogWarning("No complete offline cache available, fetching from network...");
			TarkovDevAPI.InitializeCache().Wait();
		}

		var items = TarkovDevAPI.GetItems();
		ItemScans.Enqueue(new DefaultItemScan(items[new Random().Next(items.Length)]));

		Logger.LogInfo("Initializing tarkov tracker database");
		TarkovTrackerDB = new TarkovTrackerDB();

		Logger.LogInfo("Initializing hotkey manager...");
		HotkeyManager = new HotkeyManager();
		HotkeyManager.UnregisterHotkeys();

		Logger.LogInfo("UI Ready!");

		Logger.LogInfo("Initializing RatEye...");
		SetupRatEye();

		new Thread(() => {
			Thread.Sleep(1000);
			Logger.LogInfo("Loading TarkovTracker data...");
			if (RatConfig.Tracking.TarkovTracker.Enable) {
				TarkovTrackerDB.Token = RatConfig.Tracking.TarkovTracker.Token;
				Logger.LogInfo("Loading TarkovTracker...");
				if (!TarkovTrackerDB.Init()) {
					Logger.ShowWarning("TarkovTracker API Token invalid!\n\nPlease provide a new token.");
					RatConfig.Tracking.TarkovTracker.Token = "";
					RatConfig.SaveConfig();
				}
			}

			Logger.LogInfo("Setting up timer routines...");
			_tarkovTrackerDBRefreshTimer = new Timer(RefreshTarkovTrackerDB, null, RatConfig.Tracking.TarkovTracker.RefreshTime, Timeout.Infinite);
			_scanRefreshTimer = new Timer(RefreshOverlay, null, 1000, 100);

			Logger.LogInfo("Enabling hotkeys...");
			HotkeyManager.RegisterHotkeys();

			Logger.LogInfo("Ready!");
			_ = TarkovDevAPI.InitializeCache();
		}).Start();
	}

	[MemberNotNull(nameof(RatEyeEngine))]
	internal void SetupRatEye() {
		Config.LogDebug = RatConfig.LogDebug;
		Config.Path.LogFile = "RatEyeLog.txt";
		Config.Path.TesseractLibSearchPath = AppDomain.CurrentDomain.BaseDirectory;
		RatEyeEngine = new RatEyeEngine(GetRatEyeConfig(), RatStashDatabaseFromTarkovDev());
	}

	private RatEye.Config GetRatEyeConfig(bool highlighted = true) {
		return new Config() {
			PathConfig = new Config.Path() {
				TrainedData = RatConfig.Paths.TrainedData,
				StaticIcons = RatConfig.Paths.StaticIcon,
			},
			ProcessingConfig = new Config.Processing() {
				Scale = Config.Processing.Resolution2Scale(RatConfig.ScreenWidth, RatConfig.ScreenHeight),
				Language = RatConfig.NameScan.Language,
				InspectionConfig = new Config.Processing.Inspection() {
					MarkerThreshold = 0.65f,
				},
				IconConfig = new Config.Processing.Icon() {
					UseStaticIcons = true,
					ScanMode = Config.Processing.Icon.ScanModes.TemplateMatching,
				},
				InventoryConfig = new Config.Processing.Inventory() {
					OptimizeHighlighted = highlighted,
				},
			},
		};
	}

	private Database RatStashDatabaseFromTarkovDev() {
		List<Item> rsItems = new();
		foreach (TarkovDev.GraphQL.Item i in TarkovDevAPI.GetItems()) {
			rsItems.Add(new RatStash.Item() {
				Id = i.Id,
				Name = i.Name,
				ShortName = i.ShortName,

			});
		}
		return RatStash.Database.FromItems(rsItems);
	}

	/// <summary>
	/// Perform a name scan at the give position
	/// </summary>
	/// <param name="position">Position on the screen at which to perform the scan</param>
	internal void NameScan(Vector2 position) {
		lock (NameScanLock) {
			SetScanStatusKey("ScanStatusNameTriggered");
			Logger.LogDebug("Name scanning at: " + position);
			Vector2 clickPosition = position;
			// Wait for game ui to update the click
			Thread.Sleep(50);

			// Get raw screenshot which includes the icon and text
			SetScanStatusKey("ScanStatusNameCapture");
			int markerScanSize = RatConfig.NameScan.MarkerScanSize;
			int sizeWidth = markerScanSize + RatConfig.NameScan.TextWidth;
			int sizeHeight = markerScanSize;

			position -= new Vector2(markerScanSize / 2, markerScanSize / 2);

			Bitmap screenshot = GetScreenshot(position, new Size(sizeWidth, sizeHeight));

			// Scan the item
			SetScanStatusKey("ScanStatusNameRecognizing");
			RatEye.Processing.Inspection inspection = RatEyeEngine.NewInspection(screenshot);

			if (!inspection.ContainsMarker) {
				Logger.LogDebug($"Name scan marker not found in initial area. Confidence: {inspection.MarkerConfidence}");
				int fallbackWidth = sizeWidth + (int)(120 * RatConfig.GameScale);
				int fallbackHeight = Math.Max(markerScanSize, (int)(140 * RatConfig.GameScale));
				position = clickPosition - new Vector2((int)(80 * RatConfig.GameScale), fallbackHeight / 2);
				screenshot = GetScreenshot(position, new Size(fallbackWidth, fallbackHeight));
				inspection = RatEyeEngine.NewInspection(screenshot);

				if (!inspection.ContainsMarker) {
					Logger.LogWarning($"Name scan marker not found. Confidence: {inspection.MarkerConfidence}");
					Logger.LogDebugBitmap(screenshot, "name_scan_marker_missing");
					SetScanStatusKey("ScanStatusNameMarkerMissing", true);
					return;
				}
			}

			if (inspection.Item == null) {
				SetScanStatusKey("ScanStatusNameNotRecognized", true);
				return;
			}

			float scale = RatEyeEngine.Config.ProcessingConfig.Scale;
			Bitmap marker = RatEyeEngine.Config.ProcessingConfig.InspectionConfig.Marker;
			Vector2 toolTipPosition = inspection.MarkerPosition;
			toolTipPosition += new Vector2(-(int)(marker.Width * scale), (int)(marker.Height * scale));
			toolTipPosition += position;

			TarkovDev.GraphQL.Item? matchedItem = TarkovDevAPI.FindItemById(inspection.Item.Id);
			if (matchedItem == null) {
				SetScanStatusKey("ScanStatusNameNotRecognized", true);
				return;
			}

			ItemNameScan tempNameScan = new(
				inspection,
				toolTipPosition,
				RatConfig.ToolTip.Duration,
				matchedItem);

			ItemScans.Enqueue(tempNameScan);

			SetScanStatusKey("ScanStatusNameMatched", true, tempNameScan.Item.Name);
			RefreshOverlay();
		}
	}

	/// <summary>
	/// Perform a name scan over the entire active screen
	/// </summary>
	internal void NameScanScreen(object? _ = null) {
		lock (NameScanLock) {
			SetScanStatusKey("ScanStatusAutoCapture");
			Logger.LogDebug("Name scanning screen");
			Vector2 mousePosition = UserActivityHelper.GetMousePosition();
			Rectangle bounds = Screen.AllScreens.First(screen => screen.Bounds.Contains(mousePosition)).Bounds;

			Vector2 position = new(bounds.X, bounds.Y);
			Bitmap screenshot = GetScreenshot(position, bounds.Size);

			// Scan the item
			SetScanStatusKey("ScanStatusAutoFinding");
			RatEye.Processing.MultiInspection multiInspection = RatEyeEngine.NewMultiInspection(screenshot);

			if (multiInspection.Inspections.Count == 0) {
				SetScanStatusKey("ScanStatusAutoNone", true);
				return;
			}

			int matchedCount = 0;
			foreach (RatEye.Processing.Inspection? inspection in multiInspection.Inspections) {
				if (inspection.Item == null) continue;
				TarkovDev.GraphQL.Item? matchedItem = TarkovDevAPI.FindItemById(inspection.Item.Id);
				if (matchedItem == null) continue;

				float scale = RatEyeEngine.Config.ProcessingConfig.Scale;
				Vector2 toolTipPosition = inspection.MarkerPosition;
				toolTipPosition += position;
				Bitmap marker = RatEyeEngine.Config.ProcessingConfig.InspectionConfig.Marker;
				toolTipPosition += new Vector2(0, (int)(marker.Height * scale));

				ItemNameScan tempNameScan = new(
						inspection,
						toolTipPosition,
						RatConfig.ToolTip.Duration,
						matchedItem);

				ItemScans.Enqueue(tempNameScan);
				matchedCount++;
			}
			SetScanStatusKey("ScanStatusAutoComplete", true, matchedCount);
			RefreshOverlay();
		}
	}

	/// <summary>
	/// Perform a icon scan at the given position
	/// </summary>
	/// <param name="position">Position on the screen at which to perform the scan</param>
	/// <returns><see langword="true"/> if a item was scanned successfully</returns>
	internal void IconScan(Vector2 position) {
		lock (IconScanLock) {
			SetScanStatusKey("ScanStatusIconTriggered");
			Logger.LogDebug("Icon scanning at: " + position);
			int x = position.X - RatConfig.IconScan.ScanWidth / 2;
			int y = position.Y - RatConfig.IconScan.ScanHeight / 2;

			Vector2 screenshotPosition = new(x, y);
			Size size = new(RatConfig.IconScan.ScanWidth, RatConfig.IconScan.ScanHeight);
			Bitmap screenshot = GetScreenshot(screenshotPosition, size);

			// Scan the item
			SetScanStatusKey("ScanStatusIconLocating");
			RatEye.Processing.Inventory inventory = RatEyeEngine.NewInventory(screenshot);
			RatEye.Processing.Icon? icon = inventory.LocateIcon();

			if (icon?.DetectionConfidence <= 0) {
				SetScanStatusKey("ScanStatusIconNoValidIcon", true);
				return;
			}

			if (icon?.Item == null) {
				SetScanStatusKey("ScanStatusIconNoMatch", true);
				return;
			}

			Vector2 toolTipPosition = position;
			toolTipPosition += icon.Position + icon.ItemPosition;
			toolTipPosition -= new Vector2(RatConfig.IconScan.ScanWidth, RatConfig.IconScan.ScanHeight) / 2;

			TarkovDev.GraphQL.Item? matchedItem = TarkovDevAPI.FindItemById(icon.Item.Id);
			if (matchedItem == null) {
				SetScanStatusKey("ScanStatusIconNoMatch", true);
				return;
			}

			ItemIconScan tempIconScan = new(icon, toolTipPosition, RatConfig.ToolTip.Duration, matchedItem);

			ItemScans.Enqueue(tempIconScan);
			SetScanStatusKey("ScanStatusIconMatched", true, tempIconScan.Item.Name);
			RefreshOverlay();
		}
	}

	internal void SetScanStatus(string status, bool clearSoon = false) {
		if (!RatConfig.UserInterface.ShowScanStatus) {
			ClearScanStatus();
			return;
		}

		long version = Interlocked.Increment(ref _scanStatusVersion);
		_scanStatusKey = "";
		_scanStatusArgs = Array.Empty<object>();
		ScanStatusText = status;
		OnPropertyChanged(nameof(ScanStatusText));

		if (!clearSoon) return;
		_scanStatusClearTimer?.Dispose();
		_scanStatusClearTimer = new Timer(_ => ClearScanStatus(version), null, 3500, Timeout.Infinite);
	}

	internal void SetScanStatusKey(string key, bool clearSoon = false, params object[] args) {
		if (!RatConfig.UserInterface.ShowScanStatus) {
			ClearScanStatus();
			return;
		}

		long version = Interlocked.Increment(ref _scanStatusVersion);
		_scanStatusKey = key;
		_scanStatusArgs = args ?? Array.Empty<object>();
		ScanStatusText = _localizationService.Format(key, _scanStatusArgs);
		OnPropertyChanged(nameof(ScanStatusText));

		if (!clearSoon) return;
		_scanStatusClearTimer?.Dispose();
		_scanStatusClearTimer = new Timer(_ => ClearScanStatus(version), null, 3500, Timeout.Infinite);
	}

	internal void RefreshScanStatusTranslation() {
		if (!RatConfig.UserInterface.ShowScanStatus) {
			ClearScanStatus();
			return;
		}

		if (string.IsNullOrWhiteSpace(_scanStatusKey)) {
			OnPropertyChanged();
			return;
		}
		ScanStatusText = _localizationService.Format(_scanStatusKey, _scanStatusArgs);
		OnPropertyChanged(nameof(ScanStatusText));
	}

	internal void ClearScanStatus() {
		Interlocked.Increment(ref _scanStatusVersion);
		_scanStatusClearTimer?.Dispose();
		_scanStatusClearTimer = null;
		_scanStatusKey = "";
		_scanStatusArgs = Array.Empty<object>();
		ScanStatusText = "";
		OnPropertyChanged(nameof(ScanStatusText));
	}

	private void ClearScanStatus(long version) {
		if (Interlocked.Read(ref _scanStatusVersion) != version) return;
		ClearScanStatus();
	}

	// Returns the ruff screenshot
	private Bitmap GetScreenshot(Vector2 vector2, Size size) {
		Bitmap bmp = new(size.Width, size.Height, PixelFormat.Format24bppRgb);

		try {
			using Graphics gfx = Graphics.FromImage(bmp);
			gfx.CopyFromScreen(vector2.X, vector2.Y, 0, 0, size, CopyPixelOperation.SourceCopy);
		} catch (Exception e) {
			Logger.LogWarning("Unable to capture screenshot", e);
		}

		return bmp;
	}

	private void RefreshTarkovTrackerDB(object? o = null) {
		Logger.LogInfo("Refreshing TarkovTracker DB...");
		TarkovTrackerDB.Init();
		_tarkovTrackerDBRefreshTimer.Change(RatConfig.Tracking.TarkovTracker.RefreshTime, Timeout.Infinite);
	}
	private void RefreshOverlay(object? o = null) {
		OnPropertyChanged();
	}

	protected virtual void OnPropertyChanged(string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
