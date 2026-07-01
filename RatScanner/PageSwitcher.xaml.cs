using ShuShuscanner.View;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace ShuShuscanner;

/// <summary>
/// Interaction logic for PageSwitcher.xaml
/// </summary>
public partial class PageSwitcher : Window {
	public const int DefaultWidth = 280;
	public const int DefaultHeight = 450;

	private NotifyIcon _notifyIcon = null!;
	private ContextMenuStrip _contextMenuStrip = new();
	private readonly LocalizationService _localizationService = new();

	private static PageSwitcher _instance = null!;
	public static PageSwitcher Instance => _instance ??= new PageSwitcher();

	private UserControl? activeControl;

	public PageSwitcher() {
		try {
			_instance = this;
			RatConfig.LoadConfig();

			InitializeComponent();
			RefreshWindowTitleLanguage();
			ResetWindowSize();
			Navigate(BlazorUI.Instance);
			AddJumpList();
			AddTrayIcon();

			if (RatConfig.LastWindowPositionX != int.MinValue || RatConfig.LastWindowPositionY != int.MinValue) {
				Left = RatConfig.LastWindowPositionX;
				Top = RatConfig.LastWindowPositionY;
			}
			Topmost = RatConfig.AlwaysOnTop;
			if (RatConfig.LastWindowMode == RatConfig.WindowMode.Minimal) ShowMinimalUI();
		} catch (Exception e) {
			Logger.LogError(e.Message, e);
		}
	}

	internal void ResetWindowSize() {
		SizeToContent = SizeToContent.Manual;
		Width = DefaultWidth;
		Height = DefaultHeight;

		if (RatConfig.Tracking.ShowKappaNeeds) Height += 35;

		// Avoid window stretching when using minimal menu
		MaxWidth = DefaultWidth;
	}

	internal void Navigate(UserControl nextControl, object? state = null) {
		if (!(nextControl is ISwitchable)) throw new ArgumentException("NextPage is not ISwitchable! " + nextControl.Name);

		if (activeControl != null) {
			ISwitchable activeControlSwitchable = (ISwitchable)activeControl;
			activeControlSwitchable.OnClose();
		}

		ContentControl.Content = nextControl;
		activeControl = nextControl;

		ISwitchable nextControlSwitchable = (ISwitchable)nextControl;
		if (state != null) nextControlSwitchable.UtilizeState(state);

		nextControlSwitchable.OnOpen();
	}

	protected override void OnStateChanged(EventArgs e) {
		if (RatConfig.MinimizeToTray && WindowState == WindowState.Minimized) Hide();

		base.OnStateChanged(e);
	}

	protected override void OnClosed(EventArgs e) {
		if (_notifyIcon != null) {
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}

		base.OnClosed(e);
		ExitApplication();
	}

	private void AddJumpList() {
		JumpTask showUITask = new() {
			Title = _localizationService["TrayShowMainUI"],
			Arguments = "/showUI",
			Description = _localizationService["TrayShowMainUIDescription"],
			IconResourcePath = Environment.ProcessPath,
			ApplicationPath = Environment.ProcessPath,

		};

		JumpTask showMinimalUITask = new() {
			Title = _localizationService["TrayShowMinimalUI"],
			Arguments = "/showMinimalUI",
			Description = _localizationService["TrayShowMinimalUIDescription"],
			IconResourcePath = Environment.ProcessPath,
			ApplicationPath = Environment.ProcessPath,
		};

		JumpTask showOverlayTask = new() {
			Title = _localizationService["TrayShowOverlay"],
			Arguments = "/showOverlay",
			Description = _localizationService["TrayShowOverlayDescription"],
			IconResourcePath = Environment.ProcessPath,
			ApplicationPath = Environment.ProcessPath,
		};

		JumpList jumpList = new();
		jumpList.JumpItems.Add(showUITask);
		jumpList.JumpItems.Add(showMinimalUITask);
		jumpList.JumpItems.Add(showOverlayTask);
		jumpList.ShowFrequentCategory = false;
		jumpList.ShowRecentCategory = false;

		JumpList.SetJumpList(Application.Current, jumpList);
	}

	[MemberNotNull(nameof(_notifyIcon))]
	private void AddTrayIcon() {
		_notifyIcon = new NotifyIcon {
			Visible = true,
			Icon = Properties.Resources.ShuShuLogoSmall,
		};

		_notifyIcon.ContextMenuStrip = _contextMenuStrip;
		RefreshTrayLanguage();

		_notifyIcon.MouseClick += (sender, e) => {
			if (e.Button == System.Windows.Forms.MouseButtons.Left) {
				Show();
				WindowState = WindowState.Normal;
			}
		};
	}

	internal void RefreshWindowTitleLanguage() {
		string title = _localizationService["AppTitle"];
		Title = title;
		TitleTextBlock.Text = title;
	}

	internal void RefreshTrayLanguage() {
		if (_notifyIcon == null) return;

		RefreshWindowTitleLanguage();
		_notifyIcon.Text = _localizationService["TrayShow"];
		_contextMenuStrip.Items.Clear();
		_contextMenuStrip.Items.Add(_localizationService["TrayShowMainUI"], null, OnContextMenuShowUI);
		_contextMenuStrip.Items.Add(_localizationService["TrayShowMinimalUI"], null, OnContextMenuShowMinimalUI);
		_contextMenuStrip.Items.Add(_localizationService["TrayShowOverlay"], null, OnContextMenuShowOverlay);
		_contextMenuStrip.Items.Add(_localizationService["TrayExit"], null, OnContextMenuExitApplication);
		AddJumpList();
	}

	private void OnContextMenuShowOverlay(object? sender, EventArgs e) => ShowOverlay();
	private void OnContextMenuShowUI(object? sender, EventArgs e) => ShowUI();
	private void OnContextMenuShowMinimalUI(object? sender, EventArgs e) => ShowMinimalUI();
	private void OnContextMenuExitApplication(object? sender, EventArgs e) => ExitApplication();

	internal void ShowOverlay() {
		BlazorUI.BlazorInteractableOverlay.ShowOverlay();
	}

	internal void ShowUI() {
		RatConfig.LastWindowMode = RatConfig.WindowMode.Normal;
		ResetWindowSize();
		SetBackgroundOpacity(1);
		ShowTitleBar();
		Navigate(BlazorUI.Instance);
	}

	internal void ShowMinimalUI() {
		RatConfig.LastWindowMode = RatConfig.WindowMode.Minimal;
		CollapseTitleBar();
		SizeToContent = SizeToContent.WidthAndHeight;
		SetBackgroundOpacity(RatConfig.MinimalUi.Opacity / 100f);
		Navigate(MinimalMenu.Instance);
	}

	internal void ExitApplication() {
		RatConfig.LastWindowPositionX = (int)Left;
		RatConfig.LastWindowPositionY = (int)Top;
		RatConfig.SaveConfig();
		Application.Current.Shutdown();
	}

	private void OnTitleBarMouseDown(object? sender, MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left) DragMove();
	}

	private void OnTitleBarMinimize(object? sender, RoutedEventArgs e) {
		RatConfig.LastWindowMode = RatConfig.WindowMode.Minimized;
		WindowState = WindowState.Minimized;
	}

	private void OnTitleBarMinimal(object? sender, RoutedEventArgs e) => ShowMinimalUI();

	private void OnTitleBarClose(object? sender, RoutedEventArgs e) {
		Close();
	}

	internal void CollapseTitleBar() {
		TitleBar.Visibility = Visibility.Collapsed;
	}

	internal void ShowTitleBar() {
		TitleBar.Visibility = Visibility.Visible;
	}

	internal void SetBackgroundOpacity(float opacity) {
		Background.Opacity = Math.Clamp(opacity, 1f / 510f, 1f);
	}
}
