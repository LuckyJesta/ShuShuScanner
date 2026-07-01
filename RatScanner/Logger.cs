using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ShuShuscanner;

internal static class Logger {
	private static readonly object SyncObject = new();

	private static readonly Queue<string> Backlog = new();

	private static bool Crashed = false;

	internal static void LogInfo(string message) {
		AppendToLog("[Info]  " + message);
	}

	internal static void LogWarning(string message, Exception? e = null) {
		AppendToLog("[Warning] " + message);
		if (e != null) AppendToLog(e.ToString());
	}

	internal static void LogError(Exception e) {
		Exception message = e.GetBaseException().GetBaseException();
		LogError(message.Message, e);
	}

	internal static void LogError(string message, Exception? e = null) {
		if (Crashed) return;
		Crashed = true;

		// Log the error
		string logMessage = "[Error] " + message;
		string divider = new('-', 20);
		if (e != null) logMessage += $"\n {divider} \n {e}";
		else logMessage += $"\n {divider} \n {Environment.StackTrace}";
		AppendToLog(logMessage);

		// Setup info box
		string title = "ShuShuscanner " + RatConfig.Version;

		MessageBox.Show(message + "\n\nDetails were written to the local log file.", title, MessageBoxButton.OK, MessageBoxImage.Error);

		// Exit after error is handled
		Environment.Exit(0);
	}

	internal static void LogDebugBitmap(Bitmap bitmap, string fileName = "bitmap") {
		if (RatConfig.LogDebug) bitmap.Save(GetUniquePath(RatConfig.Paths.Debug, fileName, ".png"));
	}

	internal static void LogDebug(string message = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = "") {
		if (!RatConfig.LogDebug) return;
		message = $"{caller}:{lineNumber} -> {message}";
		AppendToLog("[Debug] " + message);
	}

	internal static void ShowMessage(string message, string? title = null) {
		LogInfo(message);
		MessageBox.Show(message, title ?? "ShuShuscanner " + RatConfig.Version, MessageBoxButton.OK, MessageBoxImage.Information);
	}

	internal static void ShowWarning(string message, string? title = null) {
		LogWarning(message);
		MessageBox.Show(message, title ?? "ShuShuscanner " + RatConfig.Version, MessageBoxButton.OK, MessageBoxImage.Warning);
	}

	private static string GetUniquePath(string basePath, string fileName, string extension) {
		fileName = fileName.Replace(' ', '_');

		int index = 0;
		string uniquePath = Path.Combine(basePath, fileName + index + extension);

		while (File.Exists(uniquePath)) {
			index += 1;
			uniquePath = Path.Combine(basePath, fileName + index + extension);
		}

		Directory.CreateDirectory(Path.GetDirectoryName(uniquePath) ?? throw new NullReferenceException());
		return uniquePath;
	}

	private static void AppendToLog(string content) {
		string text = "[" + DateTime.UtcNow.ToUniversalTime().TimeOfDay + "] > " + content + "\n";
		Backlog.Enqueue(text);
		Task.Run(() => ProcessBacklog());
	}

	private static void AppendToLogRaw(string text) {
		Debug.WriteLine(text);
		File.AppendAllText(RatConfig.Paths.LogFile, text, Encoding.UTF8);
	}

	private static void ProcessBacklog() {
		lock (SyncObject) {
			for (int i = 0; i < Backlog.Count; i++) AppendToLogRaw(Backlog.Dequeue());
		}
	}

	internal static void Clear() {
		File.Delete(RatConfig.Paths.LogFile);
	}

	internal static void ClearMats(string pattern = "*.png") {
		string[] files = Directory.GetFiles(RatConfig.Paths.Data, pattern);
		foreach (string file in files) File.Delete(file);
	}

	internal static void ClearDebugMats() {
		if (!Directory.Exists(RatConfig.Paths.Debug)) return;

		string[] files = Directory.GetFiles(RatConfig.Paths.Debug, "*.png");
		foreach (string file in files)
			try {
				File.Delete(file);
			} catch (Exception) {
				LogDebug("Exception while deleting debug mats.");
			}
	}

}
