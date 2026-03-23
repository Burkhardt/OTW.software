using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<IAlbumImageCatalog>(sp =>
{
	const string defaultImageServerRoot = "/srv/ServerData/Umshadisi/otw.software/ImageServerRootDir";
	var configuration = sp.GetRequiredService<IConfiguration>();
	var imageServerRoot = configuration["ImageServer:RootDir"] ?? defaultImageServerRoot;
	return new FileSystemAlbumImageCatalog(imageServerRoot);
});
builder.Services.AddSingleton<IAlbumReviewStore>(sp =>
{
	const string defaultReviewsRoot = "/srv/ServerData/Umshadisi/otw.software/reviews";
	var configuration = sp.GetRequiredService<IConfiguration>();
	var reviewsRoot = configuration["Reviews:RootDir"] ?? defaultReviewsRoot;
	return new JsonPitAlbumReviewStore(reviewsRoot);
});
builder.Services.AddSingleton<IPersonPitStore>(sp =>
{
	const string defaultPersonPitRoot = "/srv/ServerData/Umshadisi/otw.software/personpit";
	var configuration = sp.GetRequiredService<IConfiguration>();
	var personPitRoot = configuration["PersonPit:RootDir"] ?? defaultPersonPitRoot;
	return new JsonPitPersonPitStore(personPitRoot);
});
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
app.MapPersonPitEndpoints();

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

app.MapGet("/api/albums", (string subscr, IAlbumImageCatalog catalog) =>
{
	if (string.IsNullOrWhiteSpace(subscr))
	{
		return Results.BadRequest(new { error = "Query parameter 'subscr' is required." });
	}

	var albums = catalog.ListAlbums(subscr);
	return Results.Ok(albums);
})
.WithName("ListAlbums")
.WithSummary("List all albums for a subscriber.")
.WithDescription("Albums are derived from ImageServerRootDir/{subscr}/{album}/orig. Example response: [{\"id\":\"NomsaOB-02\",\"totalImages\":37}]")
.Produces<List<AlbumDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/albums/{id}", (string id, string subscr, IAlbumImageCatalog catalog) =>
{
	if (string.IsNullOrWhiteSpace(subscr) || string.IsNullOrWhiteSpace(id))
	{
		return Results.BadRequest(new { error = "Route parameter 'id' and query parameter 'subscr' are required." });
	}

	var album = catalog.GetAlbum(subscr, id);
	if (album is null)
	{
		return Results.NotFound();
	}

	return Results.Ok(album);
})
.WithName("GetAlbum")
.WithSummary("Read one album.")
.WithDescription("Returns a filesystem-derived album if {subscr}/{id}/orig exists.")
.Produces<AlbumDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/images", (string album, string subscr, IAlbumImageCatalog catalog) =>
{
	if (string.IsNullOrWhiteSpace(subscr) || string.IsNullOrWhiteSpace(album))
	{
		return Results.BadRequest(new { error = "Query parameters 'subscr' and 'album' are required." });
	}

	var images = catalog.ListImages(subscr, album);
	return Results.Ok(images);
})
.WithName("ListImages")
.WithSummary("List image identities for one album.")
.WithDescription("Returns deduplicated image basenames (without extension) from ImageServerRootDir/{subscr}/{album}/orig. Example response: [\"NomsaOB-0222-012\",\"NomsaOB-0222-021\"]")
.Produces<List<string>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPost("/api/reviews", (ReviewVoteRequest request, IAlbumReviewStore store) =>
{
	if (request is null || string.IsNullOrWhiteSpace(request.Image) || string.IsNullOrWhiteSpace(request.Reviewer))
	{
		return Results.BadRequest(new { error = "Image and Reviewer are required." });
	}

	if (request.Vote is not (-1 or 1))
	{
		return Results.BadRequest(new { error = "Vote must be -1 or +1." });
	}

	if (!ReviewImageRef.TryParse(request.Image, out var imageRef))
	{
		return Results.BadRequest(new { error = "Image must be in format 'subscriber/imageBasename'." });
	}

	if (!AlbumId.TryDerive(imageRef.ImageBasename, out var albumId))
	{
		return Results.BadRequest(new { error = "Image basename must be at least 10 characters to derive album." });
	}

	var item = new ReviewVoteItem(
		Image: request.Image,
		Vote: request.Vote,
		Reviewer: request.Reviewer,
		Date: request.Date,
		Client: request.Client,
		Device: request.Device);

	var ok = store.Upsert(imageRef.Subscriber, albumId.Value, imageRef.ImageBasename, item);
	if (!ok)
	{
		return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
	}

	return Results.Ok(new { ok = true });
})
.WithName("UpsertReview")
.WithSummary("Post or upsert one review vote.")
.WithDescription("Stores review records in JsonPit per subscriber/album/reviewer; Vote=0 is not stored. Example request: {\"image\":\"nomsa.net/NomsaOB-0222-012\",\"vote\":1,\"reviewer\":\"Rainer\",\"date\":\"2026-03-09T18:30:00Z\",\"client\":\"ImageReview\",\"device\":\"Rainer@Burkhardt.com's iPhone\"}")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/reviews", (string album, string subscr, string reviewer, IAlbumReviewStore store) =>
{
	if (string.IsNullOrWhiteSpace(album) || string.IsNullOrWhiteSpace(subscr) || string.IsNullOrWhiteSpace(reviewer))
	{
		return Results.BadRequest(new { error = "Query parameters 'album', 'subscr', and 'reviewer' are required." });
	}

	var items = store.GetAll(subscr, album, reviewer);
	return Results.Ok(items);
})
.WithName("GetAlbumReviews")
.WithSummary("Read all review items for one album/reviewer.")
.Produces<List<ReviewVoteItem>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapDelete("/api/reviews", (string subscr, string reviewer, string image, IAlbumReviewStore store) =>
{
	if (string.IsNullOrWhiteSpace(subscr) || string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(image))
	{
		return Results.BadRequest(new { error = "Query parameters 'subscr', 'reviewer', and 'image' are required." });
	}

	if (!ReviewImageRef.TryParse(image, out var imageRef))
	{
		return Results.BadRequest(new { error = "Image must be in format 'subscriber/imageBasename'." });
	}

	if (!string.Equals(subscr, imageRef.Subscriber, StringComparison.OrdinalIgnoreCase))
	{
		return Results.BadRequest(new { error = "Query parameter 'subscr' must match the subscriber in 'image'." });
	}

	if (!AlbumId.TryDerive(imageRef.ImageBasename, out var albumId))
	{
		return Results.BadRequest(new { error = "Image basename must be at least 10 characters to derive album." });
	}

	var ok = store.Delete(subscr, albumId.Value, reviewer, imageRef.ImageBasename);
	if (!ok)
	{
		return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
	}

	return Results.Ok(new { ok = true });
})
.WithName("DeleteReview")
.WithSummary("Delete one stored review vote.")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status503ServiceUnavailable);

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

public record ReviewTask(string TaskId, string ImageUrl, string Title, string Source, string Status);

public record ReviewDecision(string TaskId, string Decision, string Note);

public record AlbumDto(string Id, int TotalImages);

public partial class Program;

public record ReviewVoteRequest(
	string Image,
	int Vote,
	string Reviewer,
	DateTimeOffset Date,
	string? Client,
	string? Device);

public record ReviewVoteItem(
	string Image,
	int Vote,
	string Reviewer,
	DateTimeOffset Date,
	string? Client,
	string? Device);

public readonly record struct ReviewImageRef(string Subscriber, string ImageBasename)
{
	public static bool TryParse(string raw, out ReviewImageRef value)
	{
		value = default;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		var idx = raw.IndexOf('/');
		if (idx <= 0 || idx >= raw.Length - 1)
		{
			return false;
		}

		var subscr = raw[..idx];
		var imageBasename = raw[(idx + 1)..];
		if (string.IsNullOrWhiteSpace(subscr) || string.IsNullOrWhiteSpace(imageBasename))
		{
			return false;
		}

		value = new ReviewImageRef(subscr, imageBasename);
		return true;
	}
}

public readonly record struct AlbumId(string Value)
{
	public static bool TryDerive(string imageBasename, out AlbumId albumId)
	{
		albumId = default;
		if (string.IsNullOrWhiteSpace(imageBasename) || imageBasename.Length < 10)
		{
			return false;
		}

		albumId = new AlbumId(imageBasename[..10]);
		return true;
	}
}

