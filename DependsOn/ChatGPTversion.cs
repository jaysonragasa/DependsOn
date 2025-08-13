//// ORIGINAL CODE FROM ChatGPT
///*
//SolutionDependencyVisualizer

//A C# Console application that:
// - Accepts a .sln file path as a command-line parameter
// - Uses MSBuild + Roslyn to parse projects, find classes and inter-class type references
// - Produces a graph JSON file and a standalone interactive HTML file (force-directed + auto-layout)
// - HTML supports dragging, auto-arrange, saving/exporting and opening/importing graphs

//Requirements / NuGet packages:
// - Microsoft.Build.Locator
// - Microsoft.CodeAnalysis.CSharp.Workspaces
// - Microsoft.CodeAnalysis.Workspaces.MSBuild
// - Newtonsoft.Json

//How to run:
// 1) dotnet add package Microsoft.Build.Locator
// 2) dotnet add package Microsoft.CodeAnalysis.CSharp.Workspaces
// 3) dotnet add package Microsoft.CodeAnalysis.Workspaces.MSBuild
// 4) dotnet add package Newtonsoft.Json

// Then run:
//   dotnet run -- "path/to/YourSolution.sln"

//Outputs (in the current directory):
// - dependency-graph.json    (nodes + links)
// - dependency-graph.html    (standalone HTML viewer; it will try to load the JSON file and fall back to embedded JSON)

//Notes:
// - This is a best-effort extractor: it finds named type references inside class declarations and members.
// - MSBuild / appropriate SDKs must be installed for MSBuildWorkspace to load the solution.
// - If loading via MSBuildWorkspace fails, the program attempts a fallback: scanning .cs files and guessing type names (basic).

//*/

//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.MSBuild;
//using Newtonsoft.Json;

//public static class Program
//{
//	private static FileInfo solutionFileInfo;

//	static async Task<int> Main(string[] args)
//	{
//		//args = new string[] { @"C:\MAUI\Pamigay\Pamigay.sln" };

//		if (args.Length < 1)
//		{
//			Console.WriteLine("Usage: SolutionDependencyVisualizer <path-to-solution.sln>");
//			return 1;
//		}

//		var slnPath = args[0];
//		if (!File.Exists(slnPath))
//		{
//			Console.WriteLine($"Solution file not found: {slnPath}");
//			return 1;
//		}

//		solutionFileInfo = new FileInfo(slnPath);

//		try
//		{
//			// Register MSBuild (required for MSBuildWorkspace)
//			var instance = Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
//			Console.WriteLine($"Registered MSBuild from: {instance?.DiscoveryType} - {instance?.VisualStudioRootPath}");
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine("Warning: MSBuild registration failed: " + ex.Message);
//			// continue - attempt to use MSBuildWorkspace anyway
//		}

//		var graph = new Graph();

//		try
//		{
//			using (var workspace = MSBuildWorkspace.Create())
//			{
//				Console.WriteLine($"Loading solution \"{solutionFileInfo.Name}\" (this may take a while)...");
//				var solution = await workspace.OpenSolutionAsync(slnPath);
//				Console.WriteLine($"Solution loaded. Building map (this may take a while)...");

//				// Build map from symbol to id
//				var typeSymbols = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
//				var symbolByName = new Dictionary<string, INamedTypeSymbol>();

//				foreach (var project in solution.Projects)
//				{
//					var compilation = await project.GetCompilationAsync();
//					if (compilation == null) continue;

//					foreach (var tree in compilation.SyntaxTrees)
//					{
//						var semantic = compilation.GetSemanticModel(tree);
//						var root = await tree.GetRootAsync();
//						var classDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
//						var filePath = tree.FilePath;

//						foreach (var td in classDecls)
//						{
//							var sym = semantic.GetDeclaredSymbol(td) as INamedTypeSymbol;
//							if (sym == null) continue;

//							string displayString = sym.ToDisplayString();

//							if (!symbolByName.ContainsKey(displayString))
//								symbolByName[displayString] = sym;
//						}
//					}
//				}

//				// assign ids
//				int id = 0;
//				foreach (var kv in symbolByName)
//				{
//					var sym = kv.Value;
//					typeSymbols[sym] = id;
//					graph.nodes.Add(new Node
//					{
//						id = id,
//						name = sym.ToDisplayString(),
//						shortName = sym.Name,
//						project = sym.ContainingAssembly?.Name,
//						FilePath = sym.Locations.FirstOrDefault()?.SourceTree?.FilePath
//					});
//					id++;
//				}

//				// find references per class
//				foreach (var project in solution.Projects)
//				{
//					var compilation = await project.GetCompilationAsync();
//					if (compilation == null) continue;

//					foreach (var tree in compilation.SyntaxTrees)
//					{
//						var semantic = compilation.GetSemanticModel(tree);
//						var root = await tree.GetRootAsync();
//						var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

//						foreach (var td in typeDecls)
//						{
//							var declared = semantic.GetDeclaredSymbol(td) as INamedTypeSymbol;
//							if (declared == null) continue;
//							if (!typeSymbols.TryGetValue(declared, out var fromId)) continue;

//							// inspect descendant nodes for identifier / object creation / member access that reference types
//							var descendantNodes = td.DescendantNodes();
//							foreach (var dn in descendantNodes)
//							{
//								ISymbol referenced = null;
//								switch (dn)
//								{
//									case IdentifierNameSyntax ins:
//										referenced = semantic.GetSymbolInfo(ins).Symbol;
//										break;
//									case ObjectCreationExpressionSyntax oces:
//										referenced = semantic.GetSymbolInfo(oces.Type).Symbol;
//										break;
//									case QualifiedNameSyntax qns:
//										referenced = semantic.GetSymbolInfo(qns).Symbol;
//										break;
//									case MemberAccessExpressionSyntax maes:
//										referenced = semantic.GetSymbolInfo(maes.Expression).Symbol;
//										break;
//									case VariableDeclarationSyntax vds:
//										referenced = semantic.GetSymbolInfo(vds.Type).Symbol;
//										break;
//								}

//								if (referenced is INamedTypeSymbol nts)
//								{
//									var full = nts.OriginalDefinition.ToDisplayString();
//									// find matching declared symbol in solution by name
//									if (symbolByName.TryGetValue(nts.ToDisplayString(), out var targetSym) || symbolByName.TryGetValue(nts.OriginalDefinition.ToDisplayString(), out targetSym))
//									{
//										if (typeSymbols.TryGetValue(targetSym, out var toId))
//										{
//											graph.AddLink(fromId, toId);
//										}
//									}
//									else
//									{
//										// sometimes the symbol includes generic args or comes from metadata; try match by name only
//										var simple = nts.ToDisplayString();
//										if (symbolByName.TryGetValue(simple, out targetSym) && typeSymbols.TryGetValue(targetSym, out var toId2))
//											graph.AddLink(fromId, toId2);
//									}
//								}
//							}
//						}
//					}
//				}

//			}
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine("Error while using Roslyn/MSBuildWorkspace: " + ex.Message);
//			Console.WriteLine("Falling back to simple file-scan heuristic.");
//			// fallback implementation: scan .cs files for "class X" and look for token occurrences of other class names
//			graph = FallbackScan(Path.GetDirectoryName(slnPath));
//		}

//		// Write outputs
//		var json = JsonConvert.SerializeObject(graph, Formatting.Indented);
//		var jsonFile = solutionFileInfo.Name + ".dependency-graph.json";
//		var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), jsonFile);
//		File.WriteAllText(jsonPath, json);
//		Console.WriteLine($"Wrote: {jsonPath}");

//		var htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "dependency-graph.html");
//		//File.WriteAllText(htmlPath, HtmlTemplate);
//		//Console.WriteLine($"Wrote: {htmlPath}");

//		Console.WriteLine("Done. Open dependency-graph.html in your browser. Use the Export/Import buttons to save or load graph JSON.");

//		return 0;
//	}

//	static Graph FallbackScan(string folder)
//	{
//		var graph = new Graph();
//		var csFiles = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
//		var names = new List<string>();
//		foreach (var f in csFiles)
//		{
//			var txt = File.ReadAllText(f);
//			// naive: find "class Name"
//			var tree = CSharpSyntaxTree.ParseText(txt);
//			var root = tree.GetRoot();
//			var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
//			foreach (var c in classDecls)
//			{
//				var n = c.Identifier.Text;
//				if (!names.Contains(n)) names.Add(n);
//			}
//		}

//		int id = 0;
//		var map = new Dictionary<string, int>();
//		foreach (var n in names)
//		{
//			map[n] = id;

//			graph.nodes.Add(new Node
//			{
//				id = id,
//				name = n,
//				shortName = n,
//				project = "fallback",
//				FilePath = n
//			});

//			id++;
//		}

//		foreach (var f in csFiles)
//		{
//			var txt = File.ReadAllText(f);
//			var tree = CSharpSyntaxTree.ParseText(txt);
//			var root = tree.GetRoot();
//			var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
//			foreach (var c in classDecls)
//			{
//				var from = c.Identifier.Text;
//				if (!map.ContainsKey(from)) continue;
//				var fromId = map[from];
//				var txtLower = txt; // naive search for other class names
//				foreach (var other in names)
//				{
//					if (other == from) continue;
//					if (txtLower.Contains(other)) graph.AddLink(fromId, map[other]);
//				}
//			}
//		}

//		return graph;
//	}

//	// simple graph model
//	class Graph
//	{
//		public List<Node> nodes { get; set; } = new List<Node>();
//		public List<Link> links { get; set; } = new List<Link>();

//		HashSet<string> seenLinks = new HashSet<string>();
//		public void AddLink(int from, int to)
//		{
//			if (from == to) return;
//			var key = from + "->" + to;
//			if (seenLinks.Add(key))
//				links.Add(new Link { source = from, target = to, value = 1 });
//		}
//	}

//	class Node
//	{
//		public int id { get; set; }
//		public string name { get; set; }
//		public string shortName { get; set; }
//		public string project { get; set; }
//		public string FilePath { get; set; }
//	}

//	class Link
//	{
//		public int source { get; set; }
//		public int target { get; set; }
//		public int value { get; set; }
//	}

//	// HTML template (uses D3 v7 from CDN). It will try to load dependency-graph.json from same folder. If not available
//	// it will use the embedded JSON passed in by the program when writing the HTML file.
//	static string HtmlTemplate = @"";
//}