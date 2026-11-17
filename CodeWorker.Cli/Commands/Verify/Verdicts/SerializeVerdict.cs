using System.Text.Json;
using System.Text.Json.Serialization;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public interface ISerializeVerdict
{
	string Serialize(Verdict verdict);
}

public class SerializeVerdict : ISerializeVerdict
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	public string Serialize(Verdict verdict)
	{
		return JsonSerializer.Serialize(verdict, JsonOptions);
	}
}
