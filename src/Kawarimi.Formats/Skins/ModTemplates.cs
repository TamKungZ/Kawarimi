namespace Kawarimi.Formats;

public static class ModTemplates
{
    public static string Csproj(string name) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <EnableDynamicLoading>true</EnableDynamicLoading>
            <AssemblyName>{name}</AssemblyName>
            <RootNamespace>{Identifier(name)}</RootNamespace>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="Kawarimi.Api">
              <HintPath>..\..\kawarimi\Kawarimi.Api.dll</HintPath>
              <Private>false</Private>
            </Reference>
          </ItemGroup>
          <Import Project="..\..\kawarimi\Kawarimi.Mod.targets" Condition="Exists('..\..\kawarimi\Kawarimi.Mod.targets')" />
        </Project>

        """;

    public static string ModClass(ModProjectInfo info) => $$"""
        using Kawarimi;

        namespace {{Identifier(info.Name)}};

        public sealed class Mod : IKawarimiMod
        {
            public void Load(IKawarimiContext context)
            {
                context.Skins.Register(new SkinDefinition
                {
                    CharacterCode = "{{info.Character}}",
                    SkinCode = "{{info.SkinCode}}",
                    Replaces = "{{info.Costume}}",
                    DisplayName = "{{info.Name}}",
                    ModelDirectory = Path.Combine(context.ModDirectory, "files"),
                });
            }
        }

        """;

    public static string Identifier(string name)
    {
        var s = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return s.Length > 0 && char.IsDigit(s[0]) ? "Mod_" + s : s;
    }
}
