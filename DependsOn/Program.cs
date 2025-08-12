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
//    private static FileInfo solutionFileInfo;

//	static async Task<int> Main(string[] args)
//	{
//        //args = new string[] { @"C:\MAUI\Pamigay\Pamigay.sln" };

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
//                        var filePath = tree.FilePath;

//						foreach (var td in classDecls)
//						{
//							var sym = semantic.GetDeclaredSymbol(td) as INamedTypeSymbol;
//							if (sym == null) continue;

//                            string displayString = sym.ToDisplayString();

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
//                    { 
//                        id = id, 
//                        name = sym.ToDisplayString(), 
//                        shortName = sym.Name, 
//                        project = sym.ContainingAssembly?.Name,
//                        FilePath = sym.Locations.FirstOrDefault()?.SourceTree?.FilePath
//                    });
//					id++;
//				}

//				//// find references per class
//				//foreach (var project in solution.Projects)
//				//{
//				//	var compilation = await project.GetCompilationAsync();
//				//	if (compilation == null) continue;

//				//	foreach (var tree in compilation.SyntaxTrees)
//				//	{
//				//		var semantic = compilation.GetSemanticModel(tree);
//				//		var root = await tree.GetRootAsync();
//				//		var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

//				//		foreach (var td in typeDecls)
//				//		{
//				//			var declared = semantic.GetDeclaredSymbol(td) as INamedTypeSymbol;
//				//			if (declared == null) continue;
//				//			if (!typeSymbols.TryGetValue(declared, out var fromId)) continue;

//				//			// inspect descendant nodes for identifier / object creation / member access that reference types
//				//			var descendantNodes = td.DescendantNodes();
//				//			foreach (var dn in descendantNodes)
//				//			{
//				//				ISymbol referenced = null;
//				//				switch (dn)
//				//				{
//				//					case IdentifierNameSyntax ins:
//				//						referenced = semantic.GetSymbolInfo(ins).Symbol;
//				//						break;
//				//					case ObjectCreationExpressionSyntax oces:
//				//						referenced = semantic.GetSymbolInfo(oces.Type).Symbol;
//				//						break;
//				//					case QualifiedNameSyntax qns:
//				//						referenced = semantic.GetSymbolInfo(qns).Symbol;
//				//						break;
//				//					case MemberAccessExpressionSyntax maes:
//				//						referenced = semantic.GetSymbolInfo(maes.Expression).Symbol;
//				//						break;
//				//					case VariableDeclarationSyntax vds:
//				//						referenced = semantic.GetSymbolInfo(vds.Type).Symbol;
//				//						break;
//				//				}

//				//				if (referenced is INamedTypeSymbol nts)
//				//				{
//				//					var full = nts.OriginalDefinition.ToDisplayString();
//				//					// find matching declared symbol in solution by name
//				//					if (symbolByName.TryGetValue(nts.ToDisplayString(), out var targetSym) || symbolByName.TryGetValue(nts.OriginalDefinition.ToDisplayString(), out targetSym))
//				//					{
//				//						if (typeSymbols.TryGetValue(targetSym, out var toId))
//				//						{
//				//							graph.AddLink(fromId, toId);
//				//						}
//				//					}
//				//					else
//				//					{
//				//						// sometimes the symbol includes generic args or comes from metadata; try match by name only
//				//						var simple = nts.ToDisplayString();
//				//						if (symbolByName.TryGetValue(simple, out targetSym) && typeSymbols.TryGetValue(targetSym, out var toId2))
//				//							graph.AddLink(fromId, toId2);
//				//					}
//				//				}
//				//			}
//				//		}
//				//	}
//				//}

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
//        var jsonFile = solutionFileInfo.Name + ".dependency-graph.json";
//        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), jsonFile);
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
//            { 
//                id = id, 
//                name = n, 
//                shortName = n, 
//                project = "fallback",
//				FilePath = n
//            });

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
//        public string FilePath { get; set; }
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

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;

class Program
{
	class Node
	{
		public int id { get; set; }
		public string name { get; set; } = "";
		public string shortName { get; set; } = "";
		public string project { get; set; } = "";
		public string FilePath { get; set; } = "";
	}

	class Link
	{
		public int source { get; set; }
		public int target { get; set; }
		public int value { get; set; }
		public string LinkType { get; set; } = "reference"; // "reference" or "inheritance"
	}

	enum ScanType
	{
		Full,
		ReferenceOnly,
		InheritanceOnly
	}

	/*
	 * 
	 * valid arguments
	 * solution file path
	 * /f = scan both Reference and Inheritance links
	 * /r = scan Reference links only
	 * /i = scan Inheritance links only
	 * 
	 */

	static ScanType GetScanType(string arg)
		=> arg switch
			{
				"/f" => ScanType.Full,
				"/r" => ScanType.ReferenceOnly,
				"/i" => ScanType.InheritanceOnly,
				_ => ScanType.Full
			};

	static void Main(string[] args)
	{
		FileInfo solFileInfo;

		//args = new string[] { @"C:\Development\Latest\Mobility\Infor.PublicSector.Mobile.sln", "/i" };

		if (args.Length != 2)
		{
			Console.WriteLine("Usage: DependsOn <path-to-sln> /f|/r|/i");
			Console.WriteLine("/f - Scan both Reference and Inheritance links");
			Console.WriteLine("/r - Scan Reference links only");
			Console.WriteLine("/i - Scan Inheritance links only");
			return;
		}

		var slnPath = args[0];
		var scanType = GetScanType((args[1]?.ToLower() ?? ""));

		solFileInfo = new FileInfo(slnPath);

		if (!solFileInfo.Exists)
		{
			Console.WriteLine($"Solution file not found: {slnPath}");
			return;
		}

		MSBuildLocator.RegisterDefaults();
		Console.WriteLine($"Loading solution \"{solFileInfo.FullName}\" (this may take a while)...");
		var workspace = MSBuildWorkspace.Create();
		var solution = workspace.OpenSolutionAsync(slnPath).Result;
		Console.WriteLine($"Solution loaded. Building map (this may take a while)...");

		var nodes = new Dictionary<int, Node>();
		var links = new List<Link>();
		var nodeLookupByName = new Dictionary<string, int>(StringComparer.Ordinal);
		int nextId = 0;
		var projectSourceDirs = solution.Projects
			.Select(p => Path.GetDirectoryName(p.FilePath) ?? "")
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		// get the projects within the solution
		foreach (var proj in solution.Projects)
		{
			var compilation = proj.GetCompilationAsync().Result;
			if (compilation == null) continue;

			// get the .cs files
			foreach (var doc in proj.Documents)
			{
				var tree = doc.GetSyntaxTreeAsync().Result;
				if (tree == null) continue;

				var model = compilation.GetSemanticModel(tree);
				if (model == null) continue;

				Console.WriteLine($"{Path.GetFileName(doc.FilePath)} Saving classes, structs, or interface to memory for linking");

				// so in one .cs file, sometimes, there are multiple classes, structs
				// or interface like in services where we put Interface and and it's
				// implementing class in one file
				var root = tree.GetRoot();

				var classes = root.DescendantNodes()
					.Select(n => model.GetDeclaredSymbol(n))
					.OfType<INamedTypeSymbol>()
					.Where(s => s.TypeKind == TypeKind.Class && s != null)
					.ToList();

				//add all classes to our nodeLookup dictionary
				if (classes.Count() == 0) continue;

				for (int i = 0; i < classes.Count(); i++)
				{
					int id = nextId++;
					INamedTypeSymbol sym = (INamedTypeSymbol)classes[i];

					var fullName = classes[i]?.ToDisplayString() ?? "";

					if (!string.IsNullOrWhiteSpace(fullName) && !nodeLookupByName.ContainsKey(fullName))
					{
						nodeLookupByName[fullName] = id;

						var shortName = string.IsNullOrWhiteSpace(sym.Name) ? sym.ToDisplayString() : sym.Name;
						// If it's a generic, include the type arguments
						if (sym.IsGenericType)
						{
							shortName = sym.Name + "<" + string.Join(", ", sym.TypeArguments.Select(t => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))) + ">";
						}
						nodes[id] = new Node
						{
							id = id,
							name = fullName,
							shortName = shortName,
							project = proj.Name,
							FilePath = doc.FilePath
						};
					}
				}

				Console.WriteLine("DONE - saving all classes to memory for linking");

				foreach (var sym in classes)
				{
					var fullName = sym.ToDisplayString();
					Console.WriteLine($"Class {fullName}");
					int nodeId = nodeLookupByName[fullName];

					var classDecl = root?.DescendantNodes()?
						.OfType<ClassDeclarationSyntax>()?
						.Where(s => s.Identifier.Text == sym.Name)?
						.FirstOrDefault();

					if (classDecl is null) continue;

					//if (!nodeLookupByName.TryGetValue(fullName, out int nodeId))
					//{
					//	nodeId = nextId++;
					//	var shortName = string.IsNullOrWhiteSpace(sym.Name) ? sym.ToDisplayString() : sym.Name;
					//	nodes[nodeId] = new Node
					//	{
					//		id = nodeId,
					//		name = fullName,
					//		shortName = shortName,
					//		project = proj.Name,
					//		FilePath = doc.FilePath
					//	};
					//	nodeLookupByName[fullName] = nodeId;
					//}

					switch (scanType)
					{
						case ScanType.Full:
							//ScanReferenceLinks(projectSourceDirs, ref nodes, root, model, ref nodeLookupByName, ref nextId, fullName, ref links, nodeId);
							//ScanInheritanceLinks((INamedTypeSymbol)sym, ref nodeLookupByName, ref nextId, ref links, ref nodes, nodeId);

							break;
						case ScanType.ReferenceOnly:
							//ScanReferenceLinks(projectSourceDirs, ref nodes, root, model, ref nodeLookupByName, ref nextId, fullName, ref links, nodeId);

							break;
						case ScanType.InheritanceOnly:
							//ScanInheritanceLinks(sym, ref nodeLookupByName, ref nextId, ref links, ref nodes, nodeId);

							var symbol = model.GetDeclaredSymbol(classDecl, default);
							if (symbol is null) continue;

							var baseType = ((INamedTypeSymbol)symbol).BaseType.OriginalDefinition;
							if (baseType is null && baseType?.SpecialType is SpecialType.System_Object)
								continue;

							var baseName = baseType.ToDisplayString();
							var result = nodeLookupByName.TryGetValue(baseName, out int baseId);

							if (baseId == 0)
							{
								continue;
							}

							if (!links.Any(l => l.source == nodeId && l.target == baseId && l.LinkType == "inheritance"))
							{
								links.Add(new Link { source = nodeId, target = baseId, LinkType = "inheritance" });
							}

							//while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
							////while (baseType != null)
							//{
							//	var baseName = baseType.ToDisplayString();
							//	_ = nodeLookupByName.TryGetValue(baseName, out int baseId);

							//	if (!links.Any(l => l.source == nodeId && l.target == baseId && l.LinkType == "inheritance"))
							//	{
							//		links.Add(new Link { source = nodeId, target = baseId, LinkType = "inheritance" });
							//	}

							//	baseType = baseType.BaseType;
							//}

							break;
					}
				}
			}
		}

		var graph = new { nodes = nodes.Values, links };
		var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
		string jsonFilename = solFileInfo.Name + ".dependency-graph.json";
		string jsonFileFullPath = Path.Combine(Directory.GetCurrentDirectory(), jsonFilename);
		File.WriteAllText(jsonFileFullPath, json);
		Console.WriteLine($"{jsonFileFullPath} created.");
	}

	static void ScanReferenceLinks(
		List<string> projectSourceDirs,
		ref Dictionary<int, Node> nodes,
		SyntaxNode root,
		SemanticModel model,
		ref Dictionary<string, int> nodeLookupByName,
		ref int nextId,
		string fullName,
		ref List<Link> links,
		int nodeId
		)
	{
		//var referencedSymbols = root.DescendantNodes()
		//					.Select(n => model.GetSymbolInfo(n).Symbol)
		//					.OfType<INamedTypeSymbol>()
		//					.Where(s => s.TypeKind == TypeKind.Class && s.ToDisplayString() != fullName)
		//					.Distinct(SymbolEqualityComparer.Default);

		var referencedSymbols = root.DescendantNodes()
			.Select(n => model.GetSymbolInfo(n).Symbol)
			.OfType<INamedTypeSymbol>()
			.Where(s => s.TypeKind == TypeKind.Class
						&& s.ToDisplayString() != fullName
						&& s.Locations.Any(loc => loc.IsInSource &&
							projectSourceDirs.Any(dir =>
								loc.SourceTree != null &&
								Path.GetDirectoryName(loc.SourceTree.FilePath)!
									.StartsWith(dir, StringComparison.OrdinalIgnoreCase))))
			.Distinct(SymbolEqualityComparer.Default);

		foreach (var refSym in referencedSymbols)
		{
			var refName = refSym.ToDisplayString();
			if (!nodeLookupByName.TryGetValue(refName, out int refId))
			{
				refId = nextId++;
				nodes[refId] = new Node
				{
					id = refId,
					name = refName,
					shortName = string.IsNullOrWhiteSpace(refSym.Name) ? refSym.ToDisplayString() : refSym.Name,
					project = refSym.ContainingAssembly?.Name ?? "",
					FilePath = ""
				};
				nodeLookupByName[refName] = refId;
			}

			if (!links.Any(l => l.source == nodeId && l.target == refId && l.LinkType == "reference"))
			{
				links.Add(new Link { source = nodeId, target = refId, LinkType = "reference" });
			}
		}
	}

	static void ScanInheritanceLinks(
		INamedTypeSymbol sym,
		ref Dictionary<string, int> nodeLookupByName,
		ref int nextId,
		ref List<Link> links,
		ref Dictionary<int, Node> nodes,
		int nodeId
		)
	{
		var baseType = sym.BaseType;
		while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
		{
			var baseName = baseType.ToDisplayString();
			if (!nodeLookupByName.TryGetValue(baseName, out int baseId))
			{
				baseId = nextId++;
				nodes[baseId] = new Node
				{
					id = baseId,
					name = baseName,
					shortName = string.IsNullOrWhiteSpace(baseType.Name) ? baseType.ToDisplayString() : baseType.Name,
					project = baseType.ContainingAssembly?.Name ?? "",
					FilePath = ""
				};
				nodeLookupByName[baseName] = baseId;
			}

			if (!links.Any(l => l.source == nodeId && l.target == baseId && l.LinkType == "inheritance"))
			{
				links.Add(new Link { source = nodeId, target = baseId, LinkType = "inheritance" });
			}

			baseType = baseType.BaseType;
		}
	}
}

