using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddRazorPages();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders =
		ForwardedHeaders.XForwardedFor |
		ForwardedHeaders.XForwardedProto |
		ForwardedHeaders.XForwardedHost;

	// Trust reverse-proxy forwarded headers (e.g., Caddy) for scheme/host resolution.
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseStaticFiles();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/openapi/v1.json", "OTW Review API v1");
});

app.MapRazorPages();

app.MapGet("/api/review/tasks", (IConfiguration configuration) =>
{
	const string defaultTasksPath = "/srv/ServerData/Umshadisi/otw.software/review/tasks.json";
	var tasksPath = configuration["ReviewTasks:FilePath"] ?? defaultTasksPath;

	var loadResult = TryLoadReviewTasks(tasksPath);
	if (loadResult.Tasks is null)
	{
		return Results.Problem(
			title: loadResult.Title,
			detail: loadResult.Detail,
			statusCode: loadResult.StatusCode);
	}

	return Results.Ok(loadResult.Tasks);
})
.WithName("GetReviewTasks");

app.MapPost("/api/review/decision", (ReviewDecision decision) =>
{
	return Results.Ok(new { ok = true });
})
.WithName("PostReviewDecision");

app.Run();

static (ReviewTask[]? Tasks, string? Title, string? Detail, int StatusCode) TryLoadReviewTasks(string tasksPath)
{
	var tasksFile = new RaiFile(tasksPath);
	var loadResult = JsonFile.TryRead<ReviewTask[]>(tasksFile);

	if (loadResult.Value is null)
	{
		var title = loadResult.ErrorKind switch
		{
			JsonFileErrorKind.NotFound => "Review tasks file not found",
			JsonFileErrorKind.InvalidJson => "Invalid review tasks JSON",
			_ => "Unable to read review tasks file"
		};

		return (
			null,
			title,
			loadResult.ErrorDetail,
			StatusCodes.Status500InternalServerError);
	}

	return (loadResult.Value, null, null, StatusCodes.Status200OK);
}

public sealed record RaiFile(string Path);

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
}

public record ReviewTask(string TaskId, string ImageUrl, string Title, string Source, string Status);

public record ReviewDecision(string TaskId, string Decision, string Note);
