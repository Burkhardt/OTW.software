using System.Collections.Generic;
using System.Linq;

public interface IAlbumImageCatalog
{
	List<AlbumDto> ListAlbums(string subscriber);
	AlbumDto? GetAlbum(string subscriber, string albumId);
	List<string> ListImages(string subscriber, string albumId);
	string? ResolveImageFile(string subscriber, string imageBasename);
}

public sealed class FileSystemAlbumImageCatalog(string imageServerRootDir) : IAlbumImageCatalog
{
	private static readonly string[] ExtensionPriority = [".webp", ".png", ".jpg", ".jpeg", ".tif", ".tiff"];
	private static readonly HashSet<string> SupportedExtensions = ExtensionPriority.ToHashSet(StringComparer.OrdinalIgnoreCase);

	public List<AlbumDto> ListAlbums(string subscriber)
	{
		var subscriberDir = SubscriberDir(subscriber);
		if (!Directory.Exists(subscriberDir))
		{
			return [];
		}

		return Directory
			.EnumerateDirectories(subscriberDir)
			.Select(Path.GetFileName)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(id => BuildAlbumDto(subscriber, id!))
			.Where(x => x is not null)
			.Select(x => x!)
			.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public AlbumDto? GetAlbum(string subscriber, string albumId)
	{
		return BuildAlbumDto(subscriber, albumId);
	}

	public List<string> ListImages(string subscriber, string albumId)
	{
		var origDir = OrigDir(subscriber, albumId);
		if (!Directory.Exists(origDir))
		{
			return [];
		}

		return EnumerateImageBasenames(origDir)
			.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public string? ResolveImageFile(string subscriber, string imageBasename)
	{
		if (!AlbumId.TryDerive(imageBasename, out var albumId))
		{
			return null;
		}

		var origDir = OrigDir(subscriber, albumId.Value);
		foreach (var ext in ExtensionPriority)
		{
			var candidate = Path.Combine(origDir, imageBasename + ext);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private AlbumDto? BuildAlbumDto(string subscriber, string albumId)
	{
		var origDir = OrigDir(subscriber, albumId);
		if (!Directory.Exists(origDir))
		{
			return null;
		}

		var total = EnumerateImageBasenames(origDir).Count();
		return new AlbumDto(albumId, total);
	}

	private IEnumerable<string> EnumerateImageBasenames(string origDir)
	{
		return Directory
			.EnumerateFiles(origDir)
			.Select(Path.GetFileName)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Where(name => SupportedExtensions.Contains(Path.GetExtension(name!)))
			.Select(name => Path.GetFileNameWithoutExtension(name!))
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase);
	}

	private string SubscriberDir(string subscriber)
	{
		return Path.Combine(imageServerRootDir, subscriber);
	}

	private string OrigDir(string subscriber, string albumId)
	{
		return Path.Combine(SubscriberDir(subscriber), albumId, "orig");
	}
}
