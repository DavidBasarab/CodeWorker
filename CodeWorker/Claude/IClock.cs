namespace FatCat.CodeWorker.Claude;

public interface IClock
{
	DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
	public DateTime UtcNow
	{
		get { return DateTime.UtcNow; }
	}
}
