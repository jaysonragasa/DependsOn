# C# Class Dependency Visualizer

A tool that scans your C# solution and generates an interactive dependency graph of classes, including base class hierarchies and class references.  
Built using Roslyn for code analysis and D3.js for visualization.

---

## Features

- Solution-wide analysis — Scans all projects and documents in a `.sln` file.
- Base class tracing — Shows class inheritance paths all the way up to the root base class.
- Reference mapping — Shows which classes reference other classes.
- Filter by root node — Focus on a specific class and its related dependencies.
- Zoom, pan, and drag — Explore the graph interactively.
- Hide unrelated nodes — Only see the classes relevant to your selected root.
- Cross-project support — Works even if classes span multiple projects in the solution.

---

## Screenshot

---

## Usage
```
DependsOn <path-to-sln> /f|/r|/i
/f - Scan both Reference and Inheritance links
/r - Scan Reference links only
/i - Scan Inheritance links only
```
### Example
```
DependsOn "C:\EnterpriseApps\POS\MakabayanPOS\MakabayanPOS.sln" \i
```
