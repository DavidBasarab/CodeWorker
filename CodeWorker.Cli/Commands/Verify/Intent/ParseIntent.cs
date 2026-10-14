using System.Text.Json;
using System.Text.Json.Serialization;
using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public interface IParseIntent
{
	Task<IntentResult> Parse(VerifyArgumentsResult arguments);
}

public class ParseIntent(IFileSystemTools fileSystemTools, IValidateIntent validateIntent) : IParseIntent
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	public async Task<IntentResult> Parse(VerifyArgumentsResult arguments)
	{
		string json;

		try
		{
			json = await fileSystemTools.ReadAllText(arguments.IntentPath);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			return Unreadable(arguments.IntentPath, exception.Message);
		}

		if (string.IsNullOrWhiteSpace(json))
		{
			return Unreadable(arguments.IntentPath, "The file is empty or missing.");
		}

		return DeserializeAndValidate(json, arguments);
	}

	private IntentResult DeserializeAndValidate(string json, VerifyArgumentsResult arguments)
	{
		IntentContract contract;

		try
		{
			contract = JsonSerializer.Deserialize<IntentContract>(json, JsonOptions);
		}
		catch (JsonException exception)
		{
			return Malformed(arguments.IntentPath, exception.Message);
		}

		if (contract == null)
		{
			return Malformed(arguments.IntentPath, "The file contains no intent object.");
		}

		return validateIntent.Validate(contract, arguments.ProductionPath);
	}

	private static IntentResult Unreadable(string intentPath, string reason)
	{
		return Failure(IntentValidationError.IntentFileUnreadable, $"Intent file could not be read: {intentPath}. {reason}");
	}

	private static IntentResult Malformed(string intentPath, string reason)
	{
		return Failure(
			IntentValidationError.MalformedIntentJson,
			$"Intent file is not a valid intent JSON object: {intentPath}. {reason}"
		);
	}

	private static IntentResult Failure(IntentValidationError error, string message)
	{
		return new IntentResult
		{
			IsValid = false,
			Error = error,
			Message = message,
			Intent = null,
		};
	}
}
