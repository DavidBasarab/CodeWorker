using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Configuration class")]
public class DateTimeLogFormatProvider : IFormatProvider, ICustomFormatter
{
	public string Format(string format, object arg, IFormatProvider formatProvider)
	{
		if (arg.GetType() != typeof(DateTimeOffset))
		{
			return HandleOtherFormats(format, arg);
		}

		var date = (DateTimeOffset)arg;

		return date.ToString("yyyy.MM.dd HH:mm:ss:fff");
	}

	public object GetFormat(Type formatType)
	{
		return formatType != typeof(ICustomFormatter) ? null : this;
	}

	private string HandleOtherFormats(string format, object arg)
	{
		if (arg is IFormattable formattable)
		{
			return formattable.ToString(format, CultureInfo.CurrentCulture);
		}

		return arg.ToString() ?? string.Empty;
	}
}
