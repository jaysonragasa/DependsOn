# DependsOn
Here's a sample preview where we are checking MAUI's Core project and then the entirety of the MAUI sln file. 
![alt text](https://github.com/jaysonragasa/DependsOn/blob/main/preview.gif?raw=true)  


## A C# Class Dependency Visualizer

A tool that scans your C# solution or project file and generates a dependency graph of classes, including base class hierarchies and class references.  
Built using Roslyn for code analysis.

## Features

- **Solution-wide analysis** — Scans all projects and documents in a `.sln` file.
- **Single project support** — Can directly scan a `.csproj` file without requiring a full solution.
- **Base class tracing** — Shows class inheritance paths all the way up to the root base class.
- **Reference mapping** — Shows which classes reference other classes.
- **Filter by root node** — Focus on a specific class and its related dependencies.
- **Cross-project support** — Works even if classes span multiple projects in the solution.

## Usage

From the command line:

```bash
DependsOn <path-to-sln-or-csproj> /f|/r|/i
```

Options:
```
/f - Scan both Reference and Inheritance links
/r - Scan Reference links only
/i - Scan Inheritance links only
```

Examples:
```bash
DependsOn MySolution.sln /f
DependsOn MyProject.csproj /i
```

---

## Output

- The generated JSON file will be saved **in the same folder as the DependsOn executable**.  
- The file name will be:
  ```
  <SolutionOrProjectFileName>.dependency-graph.json
  ```
  For example:
  ```
  MySolution.sln.dependency-graph.json
  MyProject.csproj.dependency-graph.json
  ```

## Viewing the Graph

1. Run the `DependsOn` tool to generate the JSON file.
2. Go to **[https://dependson.vercel.app/](https://dependson.vercel.app/)**.
3. Upload the generated `.dependency-graph.json` file.
4. Explore the dependency graph interactively.

## Sample JSON Output

```json
{
  "nodes": [
    {
      "id": 1,
      "name": "MyApp.ViewModels.ViewModelBase",
      "shortName": "ViewModelBase",
      "project": "Core",
      "filePath": "C:/Projects/MyApp/Core/ViewModelBase.cs"
    },
    {
      "id": 2,
      "name": "MyApp.ViewModels.ShellViewModelBase<T>",
      "shortName": "ShellViewModelBase<T>",
      "project": "Core",
      "filePath": "C:/Projects/MyApp/Core/ShellViewModelBase.cs"
    },
    {
      "id": 3,
      "name": "MyApp.ViewModels.HomeViewModel",
      "shortName": "HomeViewModel",
      "project": "UI",
      "filePath": "C:/Projects/MyApp/UI/HomeViewModel.cs"
    }
  ],
  "links": [
    { "source": 2, "target": 1, "type": "inheritance" },
    { "source": 3, "target": 2, "type": "inheritance" },
    { "source": 3, "target": 4, "type": "reference" }
  ]
}
```

---

## License

This project is licensed under the GNU GPL v3 License — see the [LICENSE](LICENSE) file for details.
