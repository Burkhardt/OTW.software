using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RaiUtils;

public static class PersonPitEndpoints
{
	private static readonly HashSet<string> AllowedComPrefs = new(StringComparer.OrdinalIgnoreCase)
	{
		"WhatsApp",
		"Email",
		"Instagram",
		"FacebookDM",
		"iMessage"
	};

	private static readonly HashSet<string> ReservedAttributes = new(StringComparer.OrdinalIgnoreCase)
	{
		"Id",
		"Modified",
		"Deleted",
		"Note"
	};

	public static IEndpointRouteBuilder MapPersonPitEndpoints(this IEndpointRouteBuilder routes)
	{
		var group = routes.MapGroup("/api/persons").WithTags("PersonPit");

		group.MapPost("/", (CreatePersonRequest request, IPersonPitStore store) =>
		{
			var validation = ValidateCreateRequest(request);
			if (validation is not null)
			{
				return validation;
			}

			var result = store.Create(new CreatePersonInput(
				request.Id.Trim(),
				NullIfWhiteSpace(request.Email),
				NullIfWhiteSpace(request.Instagram),
				NullIfWhiteSpace(request.Facebook),
				NullIfWhiteSpace(request.Phone),
				NormalizeComPref(request.ComPref)));

			if (result.Status == CreatePersonStatus.Conflict)
			{
				return Results.Conflict(new ProblemDetails
				{
					Title = "Person already exists",
					Detail = $"A person Id '{result.Id}' already exists in the person pit.",
					Status = StatusCodes.Status409Conflict
				});
			}

			return Results.Ok(new PersonMutationResponse(true, result.Id, null));
		})
		.WithId("CreatePerson")
		.WithSummary("Add a person to the PersonPit.")
		.WithDescription("Creates a JsonPit PitItem in the 'person' pit keyed by Id. This endpoint is conflict-on-existing-Id, not upsert. Request example: {\"Id\":\"Max\",\"Email\":\"max@example.com\",\"Instagram\":\"@maxmusic\",\"Facebook\":\"max.artist\",\"Phone\":\"+16195551212\",\"ComPref\":[\"WhatsApp\",\"Email\"]}. Response example: {\"ok\":true,\"Id\":\"Max\"}.")
		.Accepts<CreatePersonRequest>("application/json")
		.Produces<PersonMutationResponse>(StatusCodes.Status200OK)
		.Produces<ProblemDetails>(StatusCodes.Status409Conflict)
		.ProducesValidationProblem(StatusCodes.Status400BadRequest);

		group.MapPost("/{Id}/attributes", (string Id, AddPersonAttributeRequest request, IPersonPitStore store) =>
		{
			var validation = ValidateAttributeRequest(Id, request);
			if (validation is not null)
			{
				return validation;
			}

			var attributeValue = ConvertAttributeValue(request.Attribute.Trim(), request.Value);
			if (attributeValue.Error is not null)
			{
				return Results.ValidationProblem(attributeValue.Error);
			}

			var result = store.AddOrReplaceAttribute(Id.Trim(), request.Attribute.Trim(), attributeValue.Value!);
			if (result.Status == UpdatePersonAttributeStatus.NotFound)
			{
				return Results.NotFound(new ProblemDetails
				{
					Title = "Person not found",
					Detail = $"No person Idd '{result.Id}' exists in the person pit.",
					Status = StatusCodes.Status404NotFound
				});
			}

			return Results.Ok(new PersonMutationResponse(true, result.Id, result.Attribute));
		})
		.WithId("AddPersonAttribute")
		.WithSummary("Add or replace a Idd attribute on an existing person.")
		.WithDescription("Finds a person by Id, adds or replaces the Idd attribute, and persists the updated PitItem back into the person JsonPit. Value accepts scalar JSON, arrays, or objects. Request example: {\"Attribute\":\"Address\",\"Value\":{\"Street\":\"123 Main St\",\"City\":\"San Diego\",\"Zip\":\"92101\"}}. Response example: {\"ok\":true,\"Id\":\"Max\",\"attribute\":\"Address\"}.")
		.Accepts<AddPersonAttributeRequest>("application/json")
		.Produces<PersonMutationResponse>(StatusCodes.Status200OK)
		.Produces<ProblemDetails>(StatusCodes.Status404NotFound)
		.ProducesValidationProblem(StatusCodes.Status400BadRequest);

		group.MapGet("/{Id}", (string Id, IPersonPitStore store) =>
		{
			if (string.IsNullOrWhiteSpace(Id))
			{
				return Results.ValidationProblem(new Dictionary<string, string[]>
				{
					["Id"] = ["Id route parameter is required."]
				});
			}

			var person = store.Get(Id.Trim());
			if (person is null)
			{
				return Results.NotFound(new ProblemDetails
				{
					Title = "Person not found",
					Detail = $"No person Idd '{Id}' exists in the person pit.",
					Status = StatusCodes.Status404NotFound
				});
			}

			return Results.Ok(MapPersonResponse(person));
		})
		.WithId("GetPerson")
		.WithSummary("Return the stored data for one person.")
		.WithDescription("Returns the full current PitItem content for one person, excluding JsonPit metadata fields such as Modified, Deleted, and Note. Response example: {\"Id\":\"Max\",\"Email\":\"max@example.com\",\"Instagram\":\"@maxmusic\",\"ComPref\":[\"WhatsApp\",\"Email\"],\"Address\":{\"Street\":\"123 Main St\",\"City\":\"San Diego\",\"Zip\":\"92101\"}}.")
		.Produces<PersonResponse>(StatusCodes.Status200OK)
		.Produces<ProblemDetails>(StatusCodes.Status404NotFound)
		.ProducesValidationProblem(StatusCodes.Status400BadRequest);

		group.MapGet("/", (string? hasAttribute, IPersonPitStore store) =>
		{
			var people = store.List(NullIfWhiteSpace(hasAttribute))
				.Select(MapPersonResponse)
				.ToList();

			return Results.Ok(people);
		})
		.WithId("ListPersons")
		.WithSummary("List persons, optionally filtered by attribute existence.")
		.WithDescription("Without a query parameter, returns all persons in the PersonPit. With hasAttribute=Instagram or another attribute Id, returns every person whose current PitItem contains that attribute with a non-null value. Response example for hasAttribute=Instagram: [{\"Id\":\"Max\",\"Instagram\":\"@maxmusic\"},{\"Id\":\"Nomsa\",\"Instagram\":\"@nomsaofficial\"}].")
		.Produces<List<PersonResponse>>(StatusCodes.Status200OK);

		group.MapGet("/{Id}/communication", (string Id, IPersonPitStore store) =>
		{
			if (string.IsNullOrWhiteSpace(Id))
			{
				return Results.ValidationProblem(new Dictionary<string, string[]>
				{
					["Id"] = ["Id route parameter is required."]
				});
			}

			var communication = store.GetCommunication(Id.Trim());
			if (communication is null)
			{
				return Results.NotFound(new ProblemDetails
				{
					Title = "Person not found",
					Detail = $"No person Idd '{Id}' exists in the person pit.",
					Status = StatusCodes.Status404NotFound
				});
			}

			return Results.Ok(new CommunicationPreferenceResponse
			{
				Id = communication.Id,
				ComPref = communication.ComPref,
				Phone = communication.Phone,
				Email = communication.Email,
				Instagram = communication.Instagram,
				Facebook = communication.Facebook
			});
		})
		.WithId("GetPersonCommunication")
		.WithSummary("Return communication preferences and known contact values for one person.")
		.WithDescription("Returns ComPref together with currently known communication-related contact values already stored for that person, so a caller can see both the preferred channels and the reachable contact details. Response example: {\"Id\":\"Max\",\"ComPref\":[\"WhatsApp\",\"Email\"],\"Phone\":\"+16195551212\",\"Email\":\"max@example.com\",\"Instagram\":\"@maxmusic\",\"Facebook\":\"max.artist\"}.")
		.Produces<CommunicationPreferenceResponse>(StatusCodes.Status200OK)
		.Produces<ProblemDetails>(StatusCodes.Status404NotFound)
		.ProducesValidationProblem(StatusCodes.Status400BadRequest);

		return routes;
	}

	private static IResult? ValidateCreateRequest(CreatePersonRequest request)
	{
		if (request is null)
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["body"] = ["Request body is required."]
			});
		}

		if (string.IsNullOrWhiteSpace(request.Id))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Id"] = ["Id is required."]
			});
		}

		if (!string.IsNullOrWhiteSpace(request.Email) && new Email(request.Email).Invalid)
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Email"] = ["Email must be syntactically valid if provided."]
			});
		}

		if (request.ComPref is { Count: > 0 } && !TryValidateComPref(request.ComPref, out var comPrefError))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["ComPref"] = [comPrefError!]
			});
		}

		return null;
	}

	private static IResult? ValidateAttributeRequest(string Id, AddPersonAttributeRequest request)
	{
		if (string.IsNullOrWhiteSpace(Id))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Id"] = ["Id route parameter is required."]
			});
		}

		if (request is null)
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["body"] = ["Request body is required."]
			});
		}

		if (string.IsNullOrWhiteSpace(request.Attribute))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Attribute"] = ["Attribute is required."]
			});
		}

		if (ReservedAttributes.Contains(request.Attribute.Trim()))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Attribute"] = ["JsonPit metadata attributes cannot be overwritten through this endpoint."]
			});
		}

		if (request.Value.ValueKind == JsonValueKind.Undefined)
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["Value"] = ["Value is required."]
			});
		}

		return null;
	}

	private static (JToken? Value, Dictionary<string, string[]>? Error) ConvertAttributeValue(string attribute, JsonElement value)
	{
		if (string.Equals(attribute, "ComPref", StringComparison.OrdinalIgnoreCase))
		{
			if (value.ValueKind != JsonValueKind.Array)
			{
				return (null, new Dictionary<string, string[]>
				{
					["Value"] = ["ComPref must be a JSON array of one or more allowed values."]
				});
			}

			var values = value
				.EnumerateArray()
				.Where(element => element.ValueKind == JsonValueKind.String)
				.Select(element => element.GetString())
				.Where(element => !string.IsNullOrWhiteSpace(element))
				.Select(element => element!)
				.ToList();

			if (!TryValidateComPref(values, out var error))
			{
				return (null, new Dictionary<string, string[]>
				{
					["Value"] = [error!]
				});
			}

			return (JArray.FromObject(values), null);
		}

		return (JToken.Parse(value.GetRawText()), null);
	}

	private static bool TryValidateComPref(IReadOnlyList<string> comPref, out string? error)
	{
		if (comPref.Count == 0)
		{
			error = "ComPref must contain one or more values when provided.";
			return false;
		}

		var invalid = comPref
			.Where(value => !AllowedComPrefs.Contains(value))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (invalid.Count > 0)
		{
			error = $"Unsupported communication preference(s): {string.Join(", ", invalid)}. Allowed values: {string.Join(", ", AllowedComPrefs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}.";
			return false;
		}

		error = null;
		return true;
	}

	private static List<string> NormalizeComPref(IReadOnlyList<string>? comPref)
	{
		return comPref is null
			? []
			: comPref
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim())
				.ToList();
	}

	private static string? NullIfWhiteSpace(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static PersonResponse MapPersonResponse(JObject person)
	{
		var attributes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

		foreach (var property in person.Properties())
		{
			if (string.Equals(property.Id, "Id", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(property.Id, "Modified", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(property.Id, "Deleted", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(property.Id, "Note", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			attributes[property.Id] = ToJsonElement(ToPlainValue(property.Value));
		}

		return new PersonResponse
		{
			Id = person.Value<string>("Id") ?? string.Empty,
			Attributes = attributes
		};
	}

	private static object? ToPlainValue(JToken token)
	{
		return token switch
		{
			JObject obj => obj.ToDictionary(),
			JArray arr => arr.ToArray(),
			JValue value => value.Value,
			_ => token.ToString()
		};
	}

	private static JsonElement ToJsonElement(object? value)
	{
		var json = JsonSerializer.SerializeToUtf8Bytes(value);
		using var document = JsonDocument.Parse(json);
		return document.RootElement.Clone();
	}
}

public sealed record CreatePersonRequest
{
	public string Id { get; init; } = string.Empty;
	public string? Email { get; init; }
	public string? Instagram { get; init; }
	public string? Facebook { get; init; }
	public string? Phone { get; init; }
	public List<string> ComPref { get; init; } = [];
}

public sealed record AddPersonAttributeRequest
{
	public string Attribute { get; init; } = string.Empty;
	public JsonElement Value { get; init; }
}

public sealed record PersonMutationResponse(bool Ok, string Id, string? Attribute);

public sealed record PersonResponse
{
	public string Id { get; init; } = string.Empty;

	[JsonExtensionData]
	public IDictionary<string, JsonElement> Attributes { get; init; } = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

public sealed record CommunicationPreferenceResponse
{
	public string Id { get; init; } = string.Empty;
	public IReadOnlyList<string> ComPref { get; init; } = [];
	public string? Phone { get; init; }
	public string? Email { get; init; }
	public string? Instagram { get; init; }
	public string? Facebook { get; init; }
}