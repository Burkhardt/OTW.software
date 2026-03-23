using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JsonPit;

public interface IAlbumReviewStore
{
	bool Upsert(string subscriber, string album, string imageBasename, ReviewVoteItem item);
	List<ReviewVoteItem> GetAll(string subscriber, string album, string reviewer);
	bool Delete(string subscriber, string album, string reviewer, string imageBasename);
}

public sealed class JsonPitAlbumReviewStore(string reviewsRootDir) : IAlbumReviewStore
{
	private static readonly object Gate = new();

	public bool Upsert(string subscriber, string album, string imageBasename, ReviewVoteItem item)
	{
		lock (Gate)
		{
			try
			{
				var pit = OpenPit(subscriber, album, item.Reviewer);
				var json = JsonSerializer.Serialize(item);
				pit.Add(new PitItem(imageBasename, json, ""), true);
				pit.Save(true, true);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	public List<ReviewVoteItem> GetAll(string subscriber, string album, string reviewer)
	{
		lock (Gate)
		{
			try
			{
				var pitDir = PitDirectory(subscriber, album, reviewer);
				if (!Directory.Exists(pitDir))
				{
					return [];
				}

				var pit = new Pit(pitDir, readOnly: false);
				return pit
					.AllUndeleted()
					.Select(MapReview)
					.Where(x => x is not null)
					.Select(x => x!)
					.OrderBy(x => x.Image, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
			catch
			{
				return [];
			}
		}
	}

	public bool Delete(string subscriber, string album, string reviewer, string imageBasename)
	{
		lock (Gate)
		{
			try
			{
				var pitDir = PitDirectory(subscriber, album, reviewer);
				if (!Directory.Exists(pitDir))
				{
					return true;
				}

				var pit = new Pit(pitDir, readOnly: false);
				pit.Delete(imageBasename, "", true);
				pit.Save(true, true);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	private Pit OpenPit(string subscriber, string album, string reviewer)
	{
		var pitDir = PitDirectory(subscriber, album, reviewer);
		Directory.CreateDirectory(pitDir);
		return new Pit(pitDir, readOnly: false);
	}

	private string PitDirectory(string subscriber, string album, string reviewer)
	{
		return Path.Combine(
			reviewsRootDir,
			SanitizeSegment(subscriber),
			SanitizeSegment(album),
			SanitizeSegment(reviewer)) + "/";
	}

	private static string SanitizeSegment(string value)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var chars = value
			.Select(ch => invalid.Contains(ch) ? '_' : ch)
			.ToArray();

		return new string(chars);
	}

	private static ReviewVoteItem? MapReview(Newtonsoft.Json.Linq.JObject obj)
	{
		var image = obj.Value<string>("Image");
		var reviewer = obj.Value<string>("Reviewer");
		var client = obj.Value<string>("Client");
		var device = obj.Value<string>("Device");
		var vote = obj.Value<int?>("Vote");
		var date = obj.Value<DateTimeOffset?>("Date");

		if (string.IsNullOrWhiteSpace(image) || string.IsNullOrWhiteSpace(reviewer) || vote is null || date is null)
		{
			return null;
		}

		return new ReviewVoteItem(image, vote.Value, reviewer, date.Value, client, device);
	}
}
