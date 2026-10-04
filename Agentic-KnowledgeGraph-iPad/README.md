# Agentic Knowledge Graph - C# Learning Lab

Hands-on companion to the Pluralsight course *Agentic Knowledge Graphs*, in C# instead of Python.

## Run in GitHub Codespaces

1. Click **Code > Codespaces > Create codespace on main**.
2. Wait for the container to build (first time takes a few minutes; it starts .NET 8 and Neo4j side by side).
3. In the terminal:

   ```bash
   cd src/KgDemo
   dotnet run
   ```

## What the demo does

1. Seeds a small graph: developers, source files and services.
2. Answers: *which developers changed code that a failing service depends on?*
3. Prints the **evidence path** behind each answer - the traceability that makes graphs useful for agents.

## Project layout

```
.devcontainer/       Codespace config (.NET 8 container + Neo4j 5 container)
src/KgDemo/          Console app using the Neo4j.Driver NuGet package
```

## Connection settings

Read from environment variables (set automatically in the Codespace):
`NEO4J_URI`, `NEO4J_USER`, `NEO4J_PASSWORD`.
The password `password123` is for this throwaway demo database only.
