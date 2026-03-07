var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () =>
{
	var html = """
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1" />
	<title>OTW Software - Collection</title>
	<meta name="description" content="OTW Software merchandise and OTW 2 manual collection." />
	<link rel="preconnect" href="https://fonts.googleapis.com" />
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
	<link href="https://fonts.googleapis.com/css2?family=Barlow:wght@400;600;700&family=Space+Grotesk:wght@500;700&display=swap" rel="stylesheet" />
	<link rel="stylesheet" href="/css/otw-theme.css" />
</head>
<body>
	<div class="page-bg"></div>

	<header class="hero">
		<p class="eyebrow">OTW Archive</p>
		<h1>OTW Software Collection</h1>
		<p class="lead">
			Showcase for legacy merchandise and legacy OTW 2.4 manual box photos from early 2000.
		</p>
	</header>

	<main class="content">
		<section class="panel reveal">
			<h2>Legacy Merchandise & Product Photos</h2>
			<div class="gallery">
				<article class="photo-card">
					<div class="placeholder merch-1">
						<img class="photo-image" src="/img/OTWTasse.webp" alt="OTW branded mug" loading="lazy" />
					</div>
				</article>
				<article class="photo-card">
					<div class="placeholder merch-2">
						<img class="photo-image" src="/img/OTWBoxesManualsCDs.webp" alt="OTW boxes manuals and CDs" loading="lazy" />
					</div>
				</article>
			</div>
		</section>

		<section class="panel reveal delay">
			<h2>OTW 2.4 Manual Boxes</h2>
			<div class="gallery">
				<article class="photo-card">
					<div class="placeholder manual-1">
						<img class="photo-image" src="/img/OTW24en.webp" alt="OTW 2.4 manual English cover" loading="lazy" />
					</div>
				</article>
				<article class="photo-card">
					<div class="placeholder manual-2">
						<img class="photo-image" src="/img/OTW24de.webp" alt="OTW 2.4 manual German cover" loading="lazy" />
					</div>
				</article>
			</div>
		</section>
	</main>
</body>
</html>
""";

	return Results.Content(html, "text/html; charset=utf-8");
});

app.Run();
