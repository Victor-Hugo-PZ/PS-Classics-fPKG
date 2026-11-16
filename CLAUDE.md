# CLAUDE.md

Guia para trabalhar neste repositório. Projeto open-source herdado (autor original: SvenGDK, ver cabeçalhos dos arquivos), agora em manutenção. A base abaixo vem de uma análise completa do código; itens marcados **(não verificado)** ainda não foram testados em build real.

## O que é

Gera **fPKGs** (pacotes "fake" para PS4/PS5) de jogos **PS1, PS2 e PSP**. Não é um emulador: é uma UI que prepara uma pasta de cache (emulador + `param.sfo` + configs + ícone/fundo), gera um projeto `.gp4` e chama `orbis-pub-cmd` para produzir o `.pkg`.

Três apps independentes, **sem código compartilhado** (`Linux/makePSClassicsfPKG`: C#, Avalonia 11, .NET 8, ~2,3 mil linhas, mesma lógica do Windows portada para C#; a tabela abaixo cobre Windows e macOS):

| | `Windows/` | `macOS/` |
|---|---|---|
| Stack | VB.NET, WPF (+ WinForms só para diálogos), .NET Framework 4.8.1 | Swift 5, AppKit, storyboards, macOS 12+ |
| Entrada | `Application.xaml` → `PSClassicsfPKGBuilder.xaml(.vb)` | `AppDelegate` → `Preloader.storyboard` → `Main.storyboard` |
| Libs | DiscUtils 0.16.13 (NuGet), LibOrbisPkg (DLL local) | binários `isoinfo`, `pspdecrypt`, `sfoutil`, `unar` |
| Ferramentas externas | EXEs em `Tools\` | scripts `gengp4_*`/`make_fPKG_*` rodando dentro de um Wine empacotado |

## Layout

```
Windows/PS Classics fPKG Builder/
  AppPaths.vb                      resolve a pasta base (use sempre; ver Regras)
  PSClassicsfPKGBuilder.xaml(.vb)  janela única, 3 abas; code-behind em #Region PS1 / PS2 / PSP
  PS1Game.vb, PS2Game.vb           IDs, títulos, libcrypt (lógica fora da UI)
  Utils.vb                         resize/conversão de PNG
macOS/fPKG Builder/
  PS1/PS2/PSPClassicsViewController.swift   um controller por plataforma (PS1 e PS2 são ~idênticos)
  PreloaderViewController.swift             remove quarentena do Wine e o instala
  Utils.swift                               ToolsPath / CDrivePath / UTTypes
```

**Não estão no repositório (precisam existir para rodar):**
- Windows: `Tools\` ao lado do executável (`bin\Debug\Tools`) com `7z.exe`, `strings.exe`, `cue2toc.exe`, `Tools\PS4\{gengp4_patch.exe,crc.exe,mod-pub\orbis-pub-cmd-3.38.exe,emus\*,lua_include,ps2-configs\*}`, `pspdecrypt.exe`, `ps1ids.txt`, `ps2ids.txt`, `libcrypt.txt`; e `LibOrbisPkg.dll` (o `.vbproj` ainda aponta para o Desktop do autor original).
- macOS: pasta `Tools/` ao lado do `.app`, com `Wine Stable.app`, `Resources/wine-prefix/drive_c/...` e os scripts citados acima.
- Esses binários são proprietários/de terceiros: **nunca commitar** `Tools/`, `Cache/` nem emuladores.

## Comandos

Não há CI nem testes automatizados.

**Windows** (requer Visual Studio 2022 com workload ".NET desktop" e o `.sln`; o projeto é legado, não SDK-style) **(não verificado: ainda sem MSBuild nesta máquina)**:
```bash
nuget restore "Windows/PS Classics fPKG Builder.sln"
msbuild "Windows/PS Classics fPKG Builder.sln" /p:Configuration=Debug
```
Depois copie `Tools\` para `Windows/PS Classics fPKG Builder/bin/Debug/`.

**Checagem rápida de sintaxe sem Visual Studio** (usada para validar as alterações até agora): compilar os `.vb` num projeto SDK-style descartável (`net8.0-windows`, `UseWPF`, `UseWindowsForms`, `OptionStrict Off`, `Import` de `System.Windows*`, DiscUtils 0.16.13 por NuGet, e um stub de `LibOrbisPkg.SFO.ParamSfo`/`SfoEntryType` + shim de `My.Computer`) e rodar `dotnet build`. Isso valida a compilação do código, **não** o build real em .NET Framework 4.8.1 nem o comportamento em runtime.

**macOS** (requer Mac + Xcode 15+) **(não verificado)**:
```bash
xcodebuild -project "macOS/fPKG Builder.xcodeproj" -scheme "fPKG Builder" -configuration Debug build
```

## Convenções de nomenclatura (como o código já é; manter)

**VB.NET**
- `PascalCase` para tudo, inclusive variáveis locais e parâmetros (`GameCacheDirectory`, `PKGOutputFolder`).
- Instâncias locais descartáveis começam com `New` (`NewProcess`, `NewFileStream`); `OFD`/`FBD` para `OpenFileDialog`/`FolderBrowserDialog`.
- Controles XAML: `<Plataforma><Função><TipoDoControle>`, ex.: `PS1TitleTextBox`, `PS1NPTitleTextBox`, `BrowsePS1Disc1Button`, `PS1SkipBootlogoCheckBox`. Handlers: `<NomeDoControle>_Click`. (A aba PS2 é inconsistente: `SelectedDisc1TextBox`, `SelectedIconTextBox` sem prefixo. Em controles novos, **use sempre o prefixo da plataforma**.)
- Cada plataforma tem seu `#Region` no code-behind. Lógica de consulta/parse de IDs fica em `PS1Game`/`PS2Game` (classes com `Shared`).
- Comentários em inglês, com aspa simples: `'Create a TOC file`. Strings de UI em inglês.
- Config do emulador: linhas `--chave=valor`; comentários `#Texto` dentro do `.txt` gerado.

**Swift**
- Também `PascalCase` para propriedades, funções, outlets e actions (não segue a convenção Swift usual): `@IBOutlet weak var GameTitleTextField`, `@IBAction func BrowseDisc1`, `CreatefPKG()`, `CreateParamSFO()`.
- Outlets terminam no tipo (`...TextField`, `...CheckBox`, `...ComboBox`). Caminhos comuns via `Utils().ToolsPath` / `Utils().CDrivePath`.
- Comentários `// Texto` em inglês.

**Domínio**
- "NP Title" = ID de 9 caracteres (ex.: `SLUS00001` sem `_`/`.`), validado com `Length <> 9`.
- Cache por plataforma: `Cache\PS1fPKG`, `Cache\PS2fPKG`, `Cache\PSPfPKG` + `<nome>.gp4` ao lado. Slots de disco no PKG: `data/disc1.bin` (PS1), `image/disc01.iso` (PS2), `data/USER_L0.IMG` (PSP).
- `CONTENT_ID` = `UP9000-<NP>_00-<sufixo>`. Sufixo: `<NP>PS1FPKG` (PS1), `<NP>PSPFPKG` (PSP); no PS2 o Windows usa `<ID sem _ e .>0000001` e o macOS usa `<ID>PS2FPKG` (inconsistência entre os apps; o macOS procura o `.pkg` final por esse nome).

## Regras de arquitetura

1. **Caminhos (Windows):** use `AppPaths.BaseDirectory` (pasta do executável). **Nunca** `My.Computer.FileSystem.CurrentDirectory`: o diretório de trabalho muda conforme o atalho.
2. **Processos externos:** com `RedirectStandardOutput = True`, **leia o stdout (`ReadToEnd`) antes de `WaitForExit()`**; do contrário o processo filho trava com o buffer cheio.
3. **Streams/arquivos:** sempre em `Using` (inclusive o stream de origem de `CDReader.OpenFile`/`UdfReader.OpenFile`). Abra a origem antes de criar o arquivo de destino.
4. **Não engorde o code-behind.** Lógica nova (parse, montagem de config, edição de GP4) vai para classes separadas e testáveis, no estilo de `PS1Game`/`PS2Game`; os handlers só leem a UI e delegam.
5. **Paridade entre plataformas:** PS1/PS2/PSP e Windows/macOS repetem a mesma lógica. Ao corrigir um bug, procure o mesmo padrão nas outras cópias (vários bugs já eram copy-paste, ex.: disco 3/4 do PS1 testando o disco 2). Não unifique/refatore em massa sem combinar antes.
6. **GP4:** é editado por `String.Replace` no XML gerado por `gengp4_*` (`<?xml version="1.1"` → `1.0`, `default_id="1"` → `"0"`, inserção antes de `</files>`). Se mexer nisso, preserve esses três ajustes.
7. **macOS:** nunca force-unwrap `URL(string:)` com caminho de arquivo (caminhos com espaço retornam `nil`); use `URL(fileURLWithPath:)`. Não concatene entrada do usuário (título, caminhos) em `sh -c`; prefira `Process` com `executableURL` + `arguments`. Não bloqueie a thread principal com `sleep`.
8. **Segredos/binários:** nada de `Tools/`, `Cache/`, DLLs de terceiros ou ISOs/BINs no git.

## Bugs conhecidos ainda abertos

Windows (corrigidos nesta rodada: disco 3/4 do PS1, aspas de `--path-patches`, fixes do PS2 perdidos, `IndexOutOfRange` em `GetPS2GameID`, deadlock de stdout, handles vazados, `CurrentDirectory`):
- `cue2toc`/`.toc` só para o disco 1 no PS1, e o `.toc` é gerado dentro da pasta do jogo do usuário.
- PSP: `temp_umd_data.bin` fica na raiz e não é removido; busca do EBOOT na ISO é lenta.
- ID não detectado ("usar mesmo assim") deixa `CurrentPS2GameID` vazio → `CONTENT_ID`/caminhos inválidos; `Insert(8, ".")` do PS1 pode lançar com ID curto.
- `-vu0-opt-flags=1` com um traço só (provável typo, também no macOS).

macOS: crash com `URL(string:)!` em caminhos com espaço; config do PS2 escrita sem `\n` e com a lógica de "reiniciar emulador" invertida; fundo copiado do campo do ícone (PS2 e PSP); disco omitido do GP4 se o ISO já existe em `/Games`; `BrowseMemoryCard` é um stub vazio; `ShaderComboBox`/`LUAConfigTextField` nunca são lidos; sem paridade de features do PS2 com o Windows (bancos `.dat`, widescreen, config PS3); `launchPath`/`launch()` deprecados.

## Modernização planejada (ordem sugerida)

1. Windows estável (este arquivo, `AppPaths`, bugs acima) → 2. DiscUtils 1.x e migração para .NET 8 (WPF suporta VB) → 3. extrair a lógica de build da UI → 4. corrigir macOS (bugs de crash e config primeiro).
