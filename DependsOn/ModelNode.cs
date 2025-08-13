namespace DependsOn;

public class Node
{
	public int id { get; set; }
	public string name { get; set; } = "";
	public string shortName { get; set; } = "";
	public string project { get; set; } = "";
	public string filePath { get; set; } = "";

	public static Node Create(int id, string name, string shortName, string project, string filePath)
		=> new Node()
		{
			id = id,
			name = name,
			shortName = shortName,
			project = project,
			filePath = filePath
		};
}