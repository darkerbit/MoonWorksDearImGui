# MoonWorks Dear ImGui renderer

This project is a [Dear ImGui](https://github.com/ocornut/imgui) renderer for [MoonWorks](https://gitea.moonside.games/MoonsideGames/MoonWorks), a free cross-platform game development framework.

About self-explanatory, really :P

## How do I use it?

Copy [ImGuiRenderer.cs](MoonWorksDearImGui/ImGuiRenderer.cs) to your project, and adjust the `namespace` and the shader file paths accordingly.

See [ImGuiGame.cs](MoonWorksDearImGui/ImGuiGame.cs) for an example of usage, and `ImGuiRenderer` has doc comments.

## How do I run the example project?

1. `git clone --recursive https://github.com/darkerbit/MoonWorksDearImGui.git`
2. `./setup.sh` (run in Git Bash on Windows)
3. `dotnet run --project MoonWorksDearImGui`
