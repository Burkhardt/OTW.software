using System.Text.Json;

public enum JsonFileErrorKind
{
	None,
	NotFound,
	InvalidJson,
	ReadError
}

public static class JsonFile
{
	public static (T? Value, JsonFileErrorKind ErrorKind, string? ErrorDetail) TryRead<T>(RaiFile file)
	{
		if (!File.Exists(file.Path))
		{
			return (default, JsonFileErrorKind.NotFound, $"The configured tasks file does not exist: '{file.Path}'.");
		}

		try
		{
			var json = File.ReadAllText(file.Path);
			var value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			if (value is null)
			{
				return (default, JsonFileErrorKind.InvalidJson, $"The configured tasks file '{file.Path}' did not contain valid JSON content.");
			}

			return (value, JsonFileErrorKind.None, null);
		}
		catch (JsonException ex)
		{
			return (default, JsonFileErrorKind.InvalidJson, $"Failed to parse '{file.Path}': {ex.Message}");
		}
		catch (IOException ex)
		{
			return (default, JsonFileErrorKind.ReadError, $"Failed to read '{file.Path}': {ex.Message}");
		}
	}

	public static (T? Value, bool Ok) TryReadOrDefault<T>(RaiFile file, T fallback)
	{
		var read = TryRead<T>(file);
		if (read.ErrorKind == JsonFileErrorKind.NotFound)
		{
			return (fallback, true);
		}

		if (read.Value is null)
		{
			return (default, false);
		}

		return (read.Value, true);
	}

	public static (bool Ok, string? ErrorDetail) TryWrite<T>(RaiFile file, T value)
	{
		try
		{
			var dirPath = Path.GetDirectoryName(file.Path);
			if (!string.IsNullOrWhiteSpace(dirPath))
			{
				Directory.CreateDirectory(dirPath);
			}

			var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(file.Path, json);
			return (true, null);
		}
		catch (IOException ex)
		{
			return (false, $"Failed to write '{file.Path}': {ex.Message}");
		}
	}
}
