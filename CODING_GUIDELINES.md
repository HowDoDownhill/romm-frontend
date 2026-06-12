# Coding Guidelines

## 1. Zero Comments Rule
**CRITICAL:** Do NOT write comments in the C# code. 
- The code must be entirely self-documenting.
- Use highly descriptive, verbose, and explicit names for all classes, methods, properties, and variables so that comments are unnecessary.
- If a piece of logic seems complex enough to need a comment, refactor it into a well-named private method instead.

## 2. Godot UI and Node References
- **Never use hardcoded `GetNode<T>("Path/To/Node")` calls in the `_Ready` function for UI elements.**
- Always use `[Export]` attributes to expose UI nodes to the Godot Inspector.
- Group related exported UI nodes using the `[ExportGroup("Group Name")]` attribute to keep the Inspector organized.
- Example:
  ```csharp
  [ExportGroup("Game Details")]
  [Export] private Label _gameTitleLabel;
  [Export] private TextureRect _gameCoverArt;
  ```
- The C# script should never assume the structure of the `.tscn` file.

## 3. Asynchronous Programming
- Use standard C# `Task` and `async`/`await` for all network requests and long-running operations.
- Avoid using Godot Signals for sequential asynchronous logic unless interacting with legacy Godot nodes (like `HttpRequest` where necessary). Prefer `System.Net.Http.HttpClient` for web API calls.

## 4. Casing and Naming Conventions
- Always capitalize `RomM` consistently (e.g., `RomMAPI`, `RomMHost`).
- Use `_camelCase` for private fields.
- Use `PascalCase` for public properties, methods, and classes.
