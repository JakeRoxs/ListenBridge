# GitHub Copilot Instructions

## Priority Guidelines

This repository is a C#/.NET fork and should be treated as a standalone .NET project.

1. Use idiomatic .NET patterns and structure for all new code.
2. Keep the current codebase cleanly .NET-native and aligned with existing repository patterns.
3. Target a small CLI utility with clear separation between parser, domain, and API client.
4. If the language target is unclear, ask for clarification before generating code.

## Project Structure

- `src/<ProjectName>/` for production code
- `tests/<ProjectName>.Tests/` for unit tests
- Use folders such as `Models/`, `Services/`, `Clients/`, and `Domain/`

## C#/.NET Guidance

- Use `System.CommandLine` or a simple argument parser for CLI input.
- Use `System.Text.Json` for serialization.
- Use `HttpClient` for ListenBrainz API calls.
- Use `DateTimeOffset` for date/time handling.
- Use an HTML parser library like `HtmlAgilityPack` or `AngleSharp` for YouTube Takeout parsing.
- Keep `Program.cs` minimal and delegate business logic to services.

## Code Quality Standards

- Keep functions small, focused, and easy to test.
- Prefer self-documenting names over comments.
- Separate parsing, transformation, and HTTP submission concerns.
- Validate inputs before reading files.
- Use resilient parsing rather than failing on malformed HTML.
- Preserve request batching and API submission flow used by the original importer.

## Testing Approach

- Add tests under `tests/<ProjectName>.Tests/`.
- Cover parser behavior, date filtering, payload mapping, and request composition.
- Use a standard .NET test framework such as xUnit, NUnit, or MSTest.

## Documentation Requirements

- Keep comments minimal and purposeful.
- Document only non-obvious behavior.
- Prefer code clarity over verbose documentation.

## General Best Practices

- Follow idiomatic .NET folder structure and naming conventions.
- Avoid mixing Kotlin/JVM patterns with modern C# code.
- Keep the application simple and maintainable.
- Prioritize consistency with the current rewrite goals and repository state.
