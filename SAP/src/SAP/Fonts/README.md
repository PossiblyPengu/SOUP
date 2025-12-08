How to add a bitmap/MS-Sans-Serif-like font for the Windows98 theme

1) Choose a font file

- If you want a pixel/bitmap look, pick an appropriate open-source font (for example: "Perfect DOS VGA 437", "Px437", or other bitmap-style fonts). Check the font's license before bundling.
- If you'd rather use a system fallback, you can rely on `Tahoma` / `Microsoft Sans Serif` which are already commonly available on Windows.

2) Place the .ttf/.otf file(s)

- Put the font file in this folder: `src/SAP/Fonts/` (e.g. `src/SAP/Fonts/MS_Sans_Serif.ttf`).

3) Add the font to the project

Edit `src/SAP/SAP.csproj` and include the font resource so it gets packaged with the app. Example snippet to add inside the `<Project>` element:

```xml
<ItemGroup>
  <Resource Include="src/SAP/Fonts\MS_Sans_Serif.ttf" />
</ItemGroup>
```

4) Reference the embedded font from XAML

WPF uses a pack URI for embedded fonts. After adding the font as a `Resource`, you can reference it in `Windows98Theme.xaml` as the primary font in the `FontFamily` entry. Example:

```xaml
<!-- pack URI form: pack://application:,,,/AssemblyName;component/Path/#Font Family Name -->
<FontFamily x:Key="Win98Font">pack://application:,,,/SAP;component/Fonts/#MS Sans Serif, Tahoma, 'Segoe UI', Verdana</FontFamily>
```

Notes:
- Replace `MS Sans Serif` above with the exact font family name embedded in the TTF file. You can inspect the font's name by opening it in Windows Font Viewer.
- Keep the fallbacks (`Tahoma`, `Segoe UI`, etc.) after the embedded font so the app still looks acceptable if the embedded file isn't present.

5) Test locally

- Rebuild and run the app locally to verify the font is applied:

```powershell
cd d:\CODE\Cshp\SAP
D:\CODE\important` files\dotnet-sdk-8.0.404-win-x64\dotnet.exe build .\src\SAP\SAP.csproj -c Debug
D:\CODE\important` files\dotnet-sdk-8.0.404-win-x64\dotnet.exe run --project .\src\SAP\SAP.csproj
```

6) If you want, I can add a permissively-licensed bitmap font into the repo for you. Tell me which font (and confirm licensing) and I'll add the .ttf and wire `Windows98Theme.xaml` to prefer it.