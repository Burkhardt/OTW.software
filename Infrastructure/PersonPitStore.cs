using JsonPit;
using Newtonsoft.Json.Linq;
using OsLib;

public interface IPersonPitStore
{
	CreatePersonResult Create(CreatePersonInput input);
	UpdatePersonAttributeResult AddOrReplaceAttribute(string name, string attribute, JToken value);
	JObject? Get(string name);
	IReadOnlyList<JObject> List(string? hasAttribute = null);
	PersonCommunicationData? GetCommunication(string name);
}

public sealed class JsonPitPersonPitStore : IPersonPitStore
{
	private const string PersonPitName = "person";
	private static readonly HashSet<string> ReservedProperties = new(StringComparer.OrdinalIgnoreCase)
	{
		"Name",
		"Modified",
		"Deleted",
		"Note"
	};

	private readonly object gate = new();
	private readonly RaiPath pitDirectory;
	private readonly RaiFile canonicalPitFile;

	public JsonPitPersonPitStore(string rootDir)
	{
		var pitRoot = new RaiPath(rootDir).mkdir();
		pitDirectory = pitRoot / PersonPitName;
		canonicalPitFile = new RaiFile(pitDirectory, PersonPitName, "pit");
	}

	public CreatePersonResult Create(CreatePersonInput input)
	{
		lock (gate)
		{
			var pit = OpenWritablePit();
			if (pit.Contains(input.Name))
			{
				return new CreatePersonResult(CreatePersonStatus.Conflict, input.Name);
			}

			var item = new PitItem(input.Name);
			ApplyCreateProperties(item, input);
			pit.Add(item);
			pit.Save(force: true);

			return new CreatePersonResult(CreatePersonStatus.Created, input.Name);
		}
	}

	public UpdatePersonAttributeResult AddOrReplaceAttribute(string name, string attribute, JToken value)
	{
		lock (gate)
		{
			var pit = OpenWritablePit();
			var person = pit[name];
			if (person is null)
			{
				return new UpdatePersonAttributeResult(UpdatePersonAttributeStatus.NotFound, name, attribute);
			}

			person.SetProperty(new JObject
			{
				[attribute] = value.DeepClone()
			}.ToString());

			pit.PitItem = person;
			pit.Save(force: true);

			return new UpdatePersonAttributeResult(UpdatePersonAttributeStatus.Updated, name, attribute);
		}
	}

	public JObject? Get(string name)
	{
		lock (gate)
		{
			if (!canonicalPitFile.Exists())
			{
				return null;
			}

			var pit = OpenReadOnlyPit();
			var person = pit.Get(name);
			return person is null ? null : (JObject)person.DeepClone();
		}
	}

	public IReadOnlyList<JObject> List(string? hasAttribute = null)
	{
		lock (gate)
		{
			if (!canonicalPitFile.Exists())
			{
				return [];
			}

			var pit = OpenReadOnlyPit();
			return pit
				.AllUndeleted()
				.Select(person => (JObject)person.DeepClone())
				.Where(person => string.IsNullOrWhiteSpace(hasAttribute) || HasAttribute(person, hasAttribute))
				.OrderBy(person => person.Value<string>("Name"), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
	}

	public PersonCommunicationData? GetCommunication(string name)
	{
		var person = Get(name);
		if (person is null)
		{
			return null;
		}

		return new PersonCommunicationData(
			name,
			ReadComPref(person),
			person.Value<string>("Phone"),
			person.Value<string>("Email"),
			person.Value<string>("Instagram"),
			person.Value<string>("Facebook"));
	}

	private Pit OpenWritablePit()
	{
		pitDirectory.mkdir();
		return new Pit(pitDirectory.Path, readOnly: false, backup: false, ignoreCase: true);
	}

	private Pit OpenReadOnlyPit()
	{
		return new Pit(pitDirectory.Path, readOnly: true, unflagged: true, ignoreCase: true);
	}

	private static void ApplyCreateProperties(PitItem item, CreatePersonInput input)
	{
		if (!string.IsNullOrWhiteSpace(input.Email))
		{
			item.SetProperty(new { Email = input.Email });
		}

		if (!string.IsNullOrWhiteSpace(input.Instagram))
		{
			item.SetProperty(new { Instagram = input.Instagram });
		}

		if (!string.IsNullOrWhiteSpace(input.Facebook))
		{
			item.SetProperty(new { Facebook = input.Facebook });
		}

		if (!string.IsNullOrWhiteSpace(input.Phone))
		{
			item.SetProperty(new { Phone = input.Phone });
		}

		if (input.ComPref.Count > 0)
		{
			item.SetProperty(new { ComPref = input.ComPref.ToArray() });
		}
	}

	private static bool HasAttribute(JObject person, string attribute)
	{
		return person.Properties().Any(property =>
			!ReservedProperties.Contains(property.Name) &&
			string.Equals(property.Name, attribute, StringComparison.OrdinalIgnoreCase) &&
			property.Value.Type is not (JTokenType.Null or JTokenType.Undefined));
	}

	private static IReadOnlyList<string> ReadComPref(JObject person)
	{
		var token = person.Property("ComPref", StringComparison.OrdinalIgnoreCase)?.Value;
		if (token is not JArray array)
		{
			return [];
		}

		return array
			.Values<string>()
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Select(value => value!)
			.ToList();
	}
}

public sealed record CreatePersonInput(
	string Name,
	string? Email,
	string? Instagram,
	string? Facebook,
	string? Phone,
	IReadOnlyList<string> ComPref);

public sealed record CreatePersonResult(CreatePersonStatus Status, string Name);

public enum CreatePersonStatus
{
	Created,
	Conflict
}

public sealed record UpdatePersonAttributeResult(UpdatePersonAttributeStatus Status, string Name, string Attribute);

public enum UpdatePersonAttributeStatus
{
	Updated,
	NotFound
}

public sealed record PersonCommunicationData(
	string Name,
	IReadOnlyList<string> ComPref,
	string? Phone,
	string? Email,
	string? Instagram,
	string? Facebook);