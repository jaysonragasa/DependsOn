namespace DependsOn;

public class Node
{
	public int id { get; set; }
	public string name { get; set; } = "";
	public string shortName { get; set; } = "";
	public string project { get; set; } = "";
	public string filePath { get; set; } = "";
	public DateTime dateTimeCreated { get; set; } = DateTime.MinValue;

	public static Node Create(int id, string name, string shortName, string project, string filePath)
	{
		string file = string.Empty;
		DateTime creationTime = DateTime.MinValue;

		if (!string.IsNullOrWhiteSpace(filePath))
		{
			FileInfo fi = new FileInfo(filePath);
			file = fi.FullName;
			creationTime = fi.CreationTime;
		}

		var node = new Node()
		{
			id = id,
			name = name,
			shortName = shortName,
			project = project,
			filePath = file,
			dateTimeCreated = creationTime,
		};

		return node;
	}
}