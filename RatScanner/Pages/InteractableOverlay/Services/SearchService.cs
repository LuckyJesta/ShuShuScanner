using ShuShuscanner.TarkovDev.GraphQL;
using System;
using System.Collections.Generic;
using System.Linq;
using TTask = ShuShuscanner.TarkovDev.GraphQL.Task;

namespace ShuShuscanner.Pages.InteractableOverlay.Services;

public class SearchService {
	public async System.Threading.Tasks.Task<IEnumerable<SearchResult>> SearchMapsAsync(string value) {
		if (string.IsNullOrEmpty(value)) return Enumerable.Empty<SearchResult>();

		Func<Map, SearchResult?> filter = (map) => {
			string name = SanitizeSearch(map.Name);
			if (name == value) return new(map, 3);
			if (name.StartsWith(value)) return new(map, 15);
			if (name.Contains(value)) return new(map, 45);
			return null;
		};

		List<SearchResult> matches = new();
		await System.Threading.Tasks.Task.Run(() => {
			foreach (var map in TarkovDevAPI.GetMaps().Where(map => map != null)) {
				var match = filter(map);
				if (match?.Data == null) continue;
				matches.Add(match);
			}
		});
		return matches;
	}

	public async System.Threading.Tasks.Task<IEnumerable<SearchResult>> SearchTasksAsync(string value) {
		if (string.IsNullOrEmpty(value)) return Enumerable.Empty<SearchResult>();

		Func<TTask, SearchResult?> filter = (task) => {
			string name = SanitizeSearch(task.Name);
			string id = SanitizeSearch(task.Id);
			if (name == value) return new(task, 4);
			if (name.StartsWith(value)) return new(task, 10);
			string[] filters = value.Split(new[] { ' ' });
			if (filters.All(filter => name.Contains(filter))) return new(task, 30);
			if (name.Contains(value)) return new(task, 50);
			if (value.Length > 3 && id.StartsWith(value)) return new(task, 80);
			if (value.Length > 3 && id.Contains(value)) return new(task, 100);
			return null;
		};

		List<SearchResult> matches = new();
		await System.Threading.Tasks.Task.Run(() => {
			foreach (var task in TarkovDevAPI.GetTasks().Where(task => task != null)) {
				var match = filter(task);
				if (match?.Data == null) continue;
				matches.Add(match);
			}
		});
		return matches;
	}

	public async System.Threading.Tasks.Task<IEnumerable<SearchResult>> SearchItemsAsync(string value) {
		if (string.IsNullOrEmpty(value)) return Enumerable.Empty<SearchResult>();

		Func<Item, SearchResult?> filter = (item) => {
			string name = SanitizeSearch(item.Name);
			string shortName = SanitizeSearch(item.ShortName);
			string id = SanitizeSearch(item.Id);
			if (name == value) return new(item, 5);
			if (shortName == value) return new(item, 10);
			if (name.StartsWith(value)) return new(item, 20);
			if (shortName.StartsWith(value)) return new(item, 20);
			string[] filters = value.Split(new[] { ' ' });
			if (filters.All(filter => name.Contains(filter))) return new(item, 40);
			if (filters.All(filter => shortName.Contains(filter))) return new(item, 40);
			if (name.Contains(value)) return new(item, 60);
			if (shortName.Contains(value)) return new(item, 60);
			if (value.Length > 3 && id.StartsWith(value)) return new(item, 80);
			if (value.Length > 3 && id.Contains(value)) return new(item, 100);
			return null;
		};

		List<SearchResult> matches = new();
		await System.Threading.Tasks.Task.Run(() => {
			foreach (var item in TarkovDevAPI.GetItems().Where(item => item != null)) {
				var match = filter(item);
				if (match?.Data == null) continue;
				matches.Add(match);
			}
		});

		for (int i = 0; i < matches.Count; i++) {
			if (!(matches[i].Data is Item item)) continue;
			matches[i].Score += (item.Name?.Length ?? 0) * 0.002;
			if (item.Types != null && item.Types.Contains(ItemType.Mods))
				matches[i].Score += 5;
		}
		return matches;
	}

	public string SanitizeSearch(string? value) {
		if (string.IsNullOrEmpty(value)) return string.Empty;
		value = value.ToLower().Trim();
		value = value.Replace("-", " ");
		value = new string(value.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
		return value;
	}
}

public class SearchResult {
	public SearchResult(object data, float score) {
		Score = score;
		Data = data;
	}
	public object Data;
	public double Score;
}
