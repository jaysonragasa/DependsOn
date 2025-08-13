namespace DependsOn;

public class Link
{
	public int source { get; set; }
	public int target { get; set; }
	public int value { get; set; }
	public string linkType { get; set; } = nameof(LinkTypeEnum.Reference); // "reference" or "inheritance"
}
