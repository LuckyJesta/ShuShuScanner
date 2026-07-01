using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ShuShuscanner;

public enum UiLanguage {
	English = 0,
	Spanish = 1,
	French = 2,
	Polish = 3,
	Portuguese = 4,
	Russian = 5,
	Chinese = 6,
}

public static class UiLanguageExtensions {
	public static string ToCultureName(this UiLanguage language) {
		return language switch {
			UiLanguage.English => "English",
			UiLanguage.Spanish => "Español",
			UiLanguage.French => "Français",
			UiLanguage.Polish => "Polski",
			UiLanguage.Portuguese => "Português",
			UiLanguage.Russian => "Русский",
			UiLanguage.Chinese => "中文",
			_ => "Unknown",
		};
	}

	public static string ToCultureCode(this UiLanguage language) {
		return language switch {
			UiLanguage.English => "en",
			UiLanguage.Spanish => "es",
			UiLanguage.French => "fr",
			UiLanguage.Polish => "pl",
			UiLanguage.Portuguese => "pt",
			UiLanguage.Russian => "ru",
			UiLanguage.Chinese => "zh",
			_ => "en",
		};
	}

	public static string GetTranslationFileName(this UiLanguage language) => $"{language.ToCultureCode()}.json";
}

public class LocalizationService {
	private static Dictionary<string, string>? Translations;
	private static Dictionary<string, string>? EnglishTranslations;

	public void SetLanguage(UiLanguage language) {
		try {
			EnsureEnglishTranslations();
			var filePath = Path.Combine(RatConfig.Paths.i18nDir, language.GetTranslationFileName());
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
				Logger.LogWarning($"Translation file not found: {filePath}");
			}
			var json = File.ReadAllText(filePath);
			Translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
		} catch (Exception ex) {
			// We do not want to crash the application if the translation file is missing or malformed
			Logger.LogWarning($"Failed to load translation file for language {language.ToCultureCode()}", ex);
		}
	}

	public LocalizationService() {
		SetLanguage(RatConfig.UserInterface.Language);
	}

	public string this[string key] => Translate(key);

	public string Translate(string key) {
		if (Translations == null) return key;
		if (Translations.TryGetValue(key, out var value)) return value;
		EnsureEnglishTranslations();
		return EnglishTranslations?.TryGetValue(key, out var fallback) == true ? fallback : key;
	}

	public string Format(string key, params object[] args) {
		string format = Translate(key);
		return args == null || args.Length == 0
			? format
			: string.Format(CultureInfo.CurrentCulture, format, args);
	}

	private static void EnsureEnglishTranslations() {
		if (EnglishTranslations != null) return;
		try {
			var filePath = Path.Combine(RatConfig.Paths.i18nDir, UiLanguage.English.GetTranslationFileName());
			if (!File.Exists(filePath)) return;
			var json = File.ReadAllText(filePath);
			EnglishTranslations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
		} catch (Exception ex) {
			Logger.LogWarning("Failed to load English translation fallback", ex);
		}
	}
}
