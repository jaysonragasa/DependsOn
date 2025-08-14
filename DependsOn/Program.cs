using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;

namespace DependsOn;

class Program
{
	static int nextId = 0;
	static Dictionary<int, Node> nodes = new Dictionary<int, Node>();
	static List<Link> links = new List<Link>();
	static Dictionary<string, int> nodesLookupByName = new Dictionary<string, int>(StringComparer.Ordinal);
	static ScanTypeEnum scanTypeEnum = ScanTypeEnum.Full;

	/*
	 * 
	 * valid arguments
	 * solution file path
	 * /f = scan both Reference and Inheritance links
	 * /r = scan Reference links only
	 * /i = scan Inheritance links only
	 * 
	 */

	static ScanTypeEnum GetScanType(string arg)
		=> arg switch
		{
			"/f" => ScanTypeEnum.Full,
			"/r" => ScanTypeEnum.ReferenceOnly,
			"/i" => ScanTypeEnum.InheritanceOnly,
			_ => ScanTypeEnum.Full
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

		var filePath = args[0];
		scanTypeEnum = GetScanType((args[1]?.ToLower() ?? ""));

		solFileInfo = new FileInfo(filePath);

		if (!solFileInfo.Exists)
		{
			Console.WriteLine($"Solution file not found: {filePath}");
			return;
		}

		MSBuildLocator.RegisterDefaults();

		if (solFileInfo.Extension.ToLower() == ".sln")
			LoadSolution(solFileInfo.FullName);
		else if (solFileInfo.Extension.ToLower() == ".csproj")
			LoadProject(solFileInfo.FullName);

		Console.WriteLine($"Done mapping. Found total of {nodes.Count} nodes and {links.Count} links.");

		var graph = new { nodes = nodes.Values, links };
		var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
		string jsonFilename = solFileInfo.Name.Replace(" ", "_") + "-" + scanTypeEnum.ToString() + ".dependency-graph.json";
		string jsonFileFullPath = Path.Combine(Directory.GetCurrentDirectory(), jsonFilename);
		File.WriteAllText(jsonFileFullPath, json);
		Console.WriteLine($"{jsonFileFullPath} created.");
	}

	static void LoadSolution(string fullFilePath)
	{
		Console.WriteLine($"Loading solution \"{fullFilePath}\" (this may take a while)...");

		var workspace = MSBuildWorkspace.Create();

		var solution = workspace.OpenSolutionAsync(fullFilePath).Result;

		if (solution is not null && solution.Projects is not null && solution.Projects.Any())
		{
			var projectSourceDirs = solution.Projects
				.Select(p => Path.GetDirectoryName(p.FilePath) ?? "")
				.Where(d => !string.IsNullOrEmpty(d))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			Console.WriteLine($"Solution loaded. Building map (this may take a while)...");

			// get the projects within the solution
			foreach (var proj in solution.Projects)
				ScanProject(proj, scanTypeEnum, projectSourceDirs);
		}
		else
			Console.WriteLine($"No projects found in the solution");
	}

	static void LoadProject(string fullFilePath)
	{
		Console.WriteLine($"Loading project \"{fullFilePath}\" (this may take a while)...");

		var workspace = MSBuildWorkspace.Create();

		var project = workspace.OpenProjectAsync(fullFilePath).Result;

		if (project is not null && project.Documents is not null && project.Documents.Any())
		{
			Console.WriteLine($"Project loaded. Building map (this may take a while)...");

			var projectSourceDirs = project.Documents
				.Select(p => Path.GetDirectoryName(p.FilePath) ?? "")
				.Where(d => !string.IsNullOrEmpty(d))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			ScanProject(project, scanTypeEnum, projectSourceDirs);
		}
		else
			Console.WriteLine($"No classes found in the project");
	}

	static void ScanProject(
		Project proj,
		ScanTypeEnum scanType,
		List<string> projectSourceDirs
		)
	{
		var compilation = proj.GetCompilationAsync().Result;
		if (compilation == null) return;

		Console.WriteLine($"Found {proj.Documents.Count()} documents to scan.");

		// get the .cs files
		foreach (var doc in proj.Documents)
		{
			var tree = doc.GetSyntaxTreeAsync().Result;
			if (tree == null) continue;

			var model = compilation.GetSemanticModel(tree);
			if (model == null) continue;

			// so in one .cs file, sometimes, there are multiple classes, structs
			// or interface like in services where we put Interface and and it's
			// implementing class in one file
			var root = tree.GetRoot();
			if (root == null) continue;

			var classes = GetAllNamedTypeSymbolClasses(root, model);
			if (!classes.Any()) continue;

			Console.WriteLine($"{Path.GetFileName(doc.FilePath)} Saving classes, structs, or interface to memory for linking");

			//add all classes to our nodeLookup dictionary
			AddClassesToNodes(
				classes,
				proj.Name,
				doc?.FilePath ?? string.Empty);

			Console.WriteLine("DONE - saving all classes to memory for linking");

			foreach (var sym in classes)
			{
				var fullName = sym.ToDisplayString();
				Console.WriteLine($"Class {fullName}");
				int nodeId = nodesLookupByName[fullName];

				switch (scanType)
				{
					case ScanTypeEnum.Full:
						ScanReferenceLinks(projectSourceDirs, root, model, fullName, nodeId);
						ScanInheritanceLinks(model, sym, nodeId, root);

						break;
					case ScanTypeEnum.ReferenceOnly:
						ScanReferenceLinks(projectSourceDirs, root, model, fullName, nodeId);

						break;
					case ScanTypeEnum.InheritanceOnly:
						ScanInheritanceLinks(model, sym, nodeId, root);

						break;
				}
			}
		}
	}

	static List<INamedTypeSymbol> GetAllNamedTypeSymbolClasses(SyntaxNode root, SemanticModel model)
	{
		var classes = root.DescendantNodes()
					.Select(n => model.GetDeclaredSymbol(n))
					.OfType<INamedTypeSymbol>()
					.Where(s => s.TypeKind == TypeKind.Class && s != null)
					.ToList();

		return classes;
	}

	static SyntaxNode GetSyntaxNodeDeclarationSyntaxByClassName(SyntaxNode root, string className)
	{
		var classDecl = root?.DescendantNodes()?
			.Where(n =>
				(n is ClassDeclarationSyntax cds && cds.Identifier.Text == className) ||
				(n is RecordDeclarationSyntax rds && rds.Identifier.Text == className))
			.FirstOrDefault();

#pragma warning disable CS8603 // Possible null reference return.
		return classDecl; // Possible null reference return --- eh ano alternative?
#pragma warning restore CS8603 // Possible null reference return.
	}

	static void AddClassesToNodes(
		List<INamedTypeSymbol> classes,
		string projectName,
		string documentName
		)
	{
		for (int i = 0; i < classes.Count(); i++)
		{
			int id = nextId++;
			INamedTypeSymbol sym = (INamedTypeSymbol)classes[i];

			var fullName = classes[i]?.ToDisplayString() ?? "";

			if (!string.IsNullOrWhiteSpace(fullName) && !nodesLookupByName.ContainsKey(fullName))
			{
				nodesLookupByName[fullName] = id;

				var shortName = string.IsNullOrWhiteSpace(sym.Name) ? sym.ToDisplayString() : sym.Name;

				// If it's a generic, include the type arguments
				if (sym.IsGenericType)
					// This will add the actual generic names e.g. ViewBaseClass<TView, TViewModel>
					shortName = sym.Name + "<" + string.Join(", ", sym.TypeArguments.Select(t => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))) + ">";

				nodes[id] = Node.Create(id, fullName, shortName, projectName, documentName);
			}
		}
	}

	static void ScanReferenceLinks(
		List<string> projectSourceDirs,
		SyntaxNode root,
		SemanticModel model,
		string fullName,
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

		// now loop throuh all the classes referencing this current
		// model/class
		foreach (var refSym in referencedSymbols)
		{
			var refName = refSym.ToDisplayString();
			var result = nodesLookupByName.TryGetValue(refName, out int refId);

			if (refId == 0) return;

			if (!links.Any(l => l.source == nodeId && l.target == refId && l.linkType == nameof(LinkTypeEnum.Reference)))
			{
				links.Add(new Link { source = nodeId, target = refId, linkType = nameof(LinkTypeEnum.Reference) });
			}
		}
	}

	static void ScanInheritanceLinks(
		SemanticModel model,
		INamedTypeSymbol sym,
		int nodeId,
		SyntaxNode root
		)
	{
		var syntaxNode = GetSyntaxNodeDeclarationSyntaxByClassName(root, sym.Name);
		if (syntaxNode is null) return;

		var symbol = model.GetDeclaredSymbol(syntaxNode, default);
		if (symbol is null) return;

		var baseType = ((INamedTypeSymbol)symbol).BaseType?.OriginalDefinition ?? null;
		if (baseType is null && baseType?.SpecialType is SpecialType.System_Object)
			return;

		var baseName = baseType?.ToDisplayString() ?? null;
		if (baseName is null) return;
		var result = nodesLookupByName.TryGetValue(baseName, out int baseId);

		if (baseId == 0) return;

		if (!links.Any(l => l.source == nodeId && l.target == baseId && l.linkType == nameof(LinkTypeEnum.Inheritance)))
			links.Add(new Link { source = nodeId, target = baseId, linkType = nameof(LinkTypeEnum.Inheritance) });


		// NOTE: No need to traverse! Since we're already scanning all the classes available. 
		//       This will just duplicate the linking.
		{
			//var baseType = sym.BaseType;
			//while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
			//{
			//	var baseName = baseType.ToDisplayString();
			//	if (!nodeLookupByName.TryGetValue(baseName, out int baseId))
			//	{
			//		baseId = nextId++;
			//		nodes[baseId] = new Node
			//		{
			//			id = baseId,
			//			name = baseName,
			//			shortName = string.IsNullOrWhiteSpace(baseType.Name) ? baseType.ToDisplayString() : baseType.Name,
			//			project = baseType.ContainingAssembly?.Name ?? "",
			//			FilePath = ""
			//		};
			//		nodeLookupByName[baseName] = baseId;
			//	}

			//	if (!links.Any(l => l.source == nodeId && l.target == baseId && l.LinkType == "inheritance"))
			//	{
			//		links.Add(new Link { source = nodeId, target = baseId, LinkType = "inheritance" });
			//	}

			//	baseType = baseType.BaseType;
			//}
		}
	}
}