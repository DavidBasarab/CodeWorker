using System.Text.RegularExpressions;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public interface IValidateIntent
{
	IntentResult Validate(IntentContract intent, string productionPath);
}

public class ValidateIntent : IValidateIntent
{
	private static readonly HashSet<string> ObservableVerbs = new(StringComparer.OrdinalIgnoreCase)
	{
		"Returns",
		"Throws",
		"Calls",
		"Invokes",
		"Logs",
		"Writes",
		"Reads",
		"Saves",
		"Loads",
		"Creates",
		"Adds",
		"Removes",
		"Deletes",
		"Sets",
		"Updates",
		"Sends",
		"Raises",
		"Records",
		"Moves",
		"Reports",
		"Resolves",
		"Executes",
		"Emits",
		"Skips",
		"Stops",
	};

	private static readonly string[] NegationPrefixes = ["Does not ", "Never "];

	private static readonly Regex TestNamePattern = new(@"^[A-Za-z_][A-Za-z0-9_]*\z");

	public IntentResult Validate(IntentContract intent, string productionPath)
	{
		var expectedClass = Path.GetFileNameWithoutExtension(productionPath);

		if (string.IsNullOrWhiteSpace(intent.Why))
		{
			return Failure(
				intent,
				IntentValidationError.MissingWhy,
				"Intent is missing \"why\" — the reason this code needs to exist."
			);
		}

		if (string.IsNullOrWhiteSpace(intent.Class))
		{
			return Failure(
				intent,
				IntentValidationError.MissingClass,
				"Intent is missing \"class\" — the production class this intent describes."
			);
		}

		if (!string.Equals(intent.Class.Trim(), expectedClass, StringComparison.Ordinal))
		{
			return Failure(
				intent,
				IntentValidationError.ClassDoesNotMatchProductionFile,
				$"Intent class \"{intent.Class}\" does not match the production file {Path.GetFileName(productionPath)} — expected \"{expectedClass}\"."
			);
		}

		if (string.IsNullOrWhiteSpace(intent.Responsibility))
		{
			return Failure(
				intent,
				IntentValidationError.MissingResponsibility,
				"Intent is missing \"responsibility\" — what this class is responsible for."
			);
		}

		if (intent.Behaviors == null || intent.Behaviors.Count == 0)
		{
			return Failure(
				intent,
				IntentValidationError.NoBehaviors,
				"Intent declares no behaviors — at least one observable behavior is required."
			);
		}

		return ValidateBehaviors(intent);
	}

	private static IntentResult ValidateBehaviors(IntentContract intent)
	{
		for (var index = 0; index < intent.Behaviors.Count; index++)
		{
			var entry = intent.Behaviors[index];
			var number = index + 1;

			if (entry == null || string.IsNullOrWhiteSpace(entry.Behavior))
			{
				return Failure(intent, IntentValidationError.EmptyBehavior, $"Intent behavior {number} is empty.");
			}

			if (!IsObservable(entry.Behavior))
			{
				return Failure(
					intent,
					IntentValidationError.NonObservableBehavior,
					$"Intent behavior {number} is not observable: \"{entry.Behavior}\". Start it with an outcome verb such as Returns, Throws, Calls, or Logs."
				);
			}

			if (entry.Test != null && !IsValidTestName(entry.Test))
			{
				return Failure(
					intent,
					IntentValidationError.InvalidTestName,
					$"Intent behavior {number} names test \"{entry.Test}\", which is not a valid test method name."
				);
			}
		}

		return new IntentResult
		{
			IsValid = true,
			Error = IntentValidationError.None,
			Message = null,
			Intent = intent,
		};
	}

	private static bool IsObservable(string behavior)
	{
		var text = behavior.Trim();
		var negationPrefix = FindNegationPrefix(text);

		if (negationPrefix == null)
		{
			return ObservableVerbs.Contains(FirstWord(text));
		}

		var firstWord = FirstWord(text[negationPrefix.Length..]);

		return ObservableVerbs.Contains(firstWord) || ObservableVerbs.Contains($"{firstWord}s");
	}

	private static string FindNegationPrefix(string text)
	{
		return NegationPrefixes.FirstOrDefault(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	private static string FirstWord(string text)
	{
		return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
	}

	private static bool IsValidTestName(string test)
	{
		return TestNamePattern.IsMatch(test);
	}

	private static IntentResult Failure(IntentContract intent, IntentValidationError error, string message)
	{
		return new IntentResult
		{
			IsValid = false,
			Error = error,
			Message = message,
			Intent = intent,
		};
	}
}
