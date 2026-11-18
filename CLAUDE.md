# CLAUDE.md

Guia para trabalhar neste repositório. Projeto open-source herdado (autor original: SvenGDK) e agora mantido neste fork. Itens marcados **(não verificado)** ainda não foram testados em build real.

## O que é

Gera **fPKGs** (pacotes "fake" para PS4/PS5) de jogos **PS1, PS2 e PSP**. Não é um emulador: é uma UI que prepara uma pasta de cache (emulador + `param.sfo` + configs + ícone/fundo), gera um projeto `.gp4` e chama `orbis-pub-cmd` para produzir o `.pkg`.

Uma biblioteca de lógica compartilhada e duas UIs com as mesmas telas:

| | `Shared/PSClassics.Core` | `Windows/PS Classics fPKG Builder` | `CrossPlatform/PSClassicsfPKGBuilder` |
|---|---|---|---|
| O quê | toda a lógica de build (sem UI) | app Windows | app Linux e macOS (também compila e roda no Windows) |
| Stack | VB.NET, `net10.0` | VB.NET, WPF + WPF-UI 4.x, `net10.0-windows` | C#, Avalonia 12 + Material.Icons, `net10.0` |
| Ferramentas Sony | `ToolRunner`: direto no Windows, via **Wine** no Linux/macOS | | |

`legacy/Linux` (Avalonia 11) e `legacy/macOS` (Swift) são os apps antigos, substituídos por `CrossPlatform/`. Só referência: não corrigir, não compilar no CI.

## Layout

```
Shared/PSClassics.Core/                      lógica (RootNamespace PS_Classics_fPKG_Builder)
  PackageBuilders.vb                         PS1/PS2/PSP PackageBuilder + *BuildRequest (orquestra o build inteiro)
  EmulatorConfigs.vb SfoBuilder.vb Gp4Editor.vb
  IsoTools.vb PspTools.vb CueToc.vb          ISO/UDF (DiscUtils), CRC (gerenciado), bancos .dat, EBOOT do PSP, TOC (cue2toc)
  PS1Game.vb PS2Game.vb                      IDs (strings/7z reimplementados em código), títulos, libcrypt
  ImageConverter.vb ImageFormats.vb          SkiaSharp: qualquer formato -> PNG 24 bits (PngWriter próprio), dicas do preview
  Platform.vb                                Platform (OS, caminho Wine Z:\), ToolRunner (exe direto ou via wine), Wine (achar, status, setup)
  ProcessRunner.vb                           ProcessRunner + PkgTools (gengp4 / orbis-pub-cmd)
  PackageVerifier.vb                         checagem de .pkg (img_verify + estrutura via LibOrbisPkg)
  Localizer.vb Localization/pt-BR.tsv         tradução da UI (texto em inglês = chave; TAB; tradução). Usado pelas duas UIs
  AppSettings.vb BuildHistory.vb             preferências e histórico (JSON em AppPaths.DataDirectory)
  BuildPreflight.vb ErrorExplainer.vb        checagens antes do build (com a correção) e erros explicados em linguagem simples
  EmulatorPresets.vb CoverArt.vb             predefinições do emulador; capas (xlenore/psx-covers e ps2-covers) e imagens do disco PSP
  UpdateChecker.vb DroppedFiles.vb           aviso de nova versão (API do GitHub); classificação de arquivos arrastados
  CoverSources.vb                            Buscar capa (PS1/PS2): coleção por ID -> nome do banco Redump pelo serial -> título (libretro)
  DiscImages.vb                              CUE de um disco (acha o .cue que lista o .bin) e junção de discos com um BIN por faixa (cue2toc lê um BIN só)
  ArchiveImport.vb                           .zip/.7z arrastado: extração com 7z e achado dos arquivos do jogo (mesmo caminho do arrastar)
  ArchiveInspector.vb                        olha dentro do .zip/.7z antes de extrair (sem jogo, faixa faltando no .cue, senha, espaço)
  CsoReader.vb SfoReader.vb                  PSP .cso (CISO) -> .iso; PARAM.SFO (título do PSP)
  BatchBuilder.vb                            fila: BatchBuilder (zip -> extrai -> detecta -> capa -> build com predefinição) e BatchQueue (lista da janela Fila)
  AppPaths.vb FileUtils.vb
Shared/PSClassics.Core.Tests/                xUnit (C#): unidade + integração com as ferramentas reais
Windows/PS Classics fPKG Builder/            app WPF: PSClassicsfPKGBuilder.xaml(.vb), DashPreview, FieldLabel, StepBar, UiTranslator, janelas (Guide, PackageReport, BuildResult, History, Settings)
Windows/PS Classics fPKG Builder.Tests/      testes de UI do WPF (preview, FieldLabel)
Windows/PkgCheck/                            PkgCheck (linha de comando, net10.0, roda em todo OS); vai em todo release
CrossPlatform/PSClassicsfPKGBuilder/         app Avalonia: MainWindow, UiTranslator, Controls/ (DashPreview, FieldLabel, StepBar), Dialogs/ (Guide, PackageReport, BuildResult, History, Settings, WineSetup, MessageDialog), Styles.axaml
scripts/build-release.ps1                    release Windows (zip, 7-Zip)
scripts/build-release.sh                     release Linux (zip) e macOS (.app ad-hoc assinado em zip)
  (release: cada programa é UM arquivo autocontido (`PublishSingleFile`); a pasta Tools é copiada pelo script, não pelo MSBuild, que copiava arquivos faltando. O zip tem uma pasta de topo com o app, o PkgCheck, Tools e README.)
.github/workflows/ci.yml                     Windows + Linux/macOS (build e testes) + integração no Linux com Wine
.github/workflows/release-packages.yml       manual: gera os 4 pacotes como artefatos (não publica release)
external/LibOrbisPkg/                        submódulo git (LGPL-3.0)
Tools/                                       NÃO versionada (.gitignore): ferramentas e emuladores de terceiros
```

`Tools/` vem do zip de um release Windows (deste fork ou o `v1` original). São binários proprietários/de terceiros: **nunca commitar** `Tools/`, `Cache/`, emuladores, DLLs ou ISOs/BINs.

## Comandos

Requer o .NET 10 SDK.

```bash
git clone --recurse-submodules <repo>        # ou: git submodule update --init
dotnet build "Windows/PS Classics fPKG Builder.sln"      # Windows (WPF + Core + testes)
dotnet test  "Windows/PS Classics fPKG Builder.sln"
dotnet build CrossPlatform/PSClassicsfPKGBuilder.sln     # Avalonia + Core + testes (qualquer OS)
dotnet test  CrossPlatform/PSClassicsfPKGBuilder.sln
dotnet run --project CrossPlatform/PSClassicsfPKGBuilder
```

- O build copia `Tools/` (raiz do repo) para a pasta do executável quando ela existe. `-p:CopyToolsToOutput=false` desliga isso.
- Integração: `[RequiresToolsFact]` (precisa de `Tools/`), `[RequiresToolRunnerFact]` (Tools + Wine pronto no Linux/macOS), `[WindowsToolsFact]` (compara com `strings.exe`/`crc.exe`/`7z.exe` originais, só Windows). Sem os requisitos são **ignorados**, não falham. `PackageVerifierIntegration` é lento (minutos).
- `TempWorkspace` aponta `AppPaths.BaseDirectory`/`CacheDirectory` para uma pasta temporária (junction no Windows, symlink no Unix para `Tools/`). Testes não rodam em paralelo (estado estático).
- Releases: ver README ("Building a release"). Não publicar release sem o usuário testar os pacotes.

## Convenções de nomenclatura (como o código já é; manter)

**VB.NET**
- `PascalCase` para tudo, inclusive locais e parâmetros (`GameCacheDirectory`, `PKGOutputFolder`). Instâncias locais descartáveis começam com `New` (`NewProcess`); `OFD`/`FBD` para os diálogos.
- Controles XAML: `<Plataforma><Função><Tipo>` (`PS1TitleTextBox`, `BrowsePS1Disc1Button`); handlers `<Controle>_Click`. Na aba PS2 alguns controles não têm prefixo (`SelectedDisc1TextBox`); em controles novos **use sempre o prefixo da plataforma**.
- Classes de Core: substantivos (`Gp4Editor`, `SfoBuilder`), métodos `Shared`; opções em classes `*Options`/`*BuildRequest`.
- Comentários em inglês, com aspa simples (`'Create a TOC file`) e `'''` para XML docs. Strings de UI em inglês.
- Config do emulador: linhas `--chave=valor`; comentários `#Texto`.

**C# (Avalonia e testes)**: convenções padrão de C# (`_camelCase` em campos, `PascalCase` em métodos); os `x:Name` dos controles são **os mesmos do WPF**. Testes: um arquivo por área, `NomeDaClasseTests`, métodos `Metodo_ComportamentoEsperado`; bugs corrigidos ganham `// Regression: ...`.

**Domínio**
- "NP Title" = ID de 9 caracteres (`SLUS00594`, sem `_`/`.`), validado com `Length <> 9`.
- Cache: `Cache/PS1fPKG`, `PS2fPKG`, `PSPfPKG` + `<nome>.gp4` ao lado (no macOS o Cache fica em `~/Library/Application Support/PSClassicsfPKGBuilder`, porque o `.app` não é gravável). Discos no PKG: `data/disc1.bin` (PS1), `image/disc01.iso` (PS2), `data/USER_L0.IMG` (PSP).
- `CONTENT_ID` = `UP9000-<NP>_00-<sufixo>`: `<NP>PS1FPKG`, `<NP>PSPFPKG`, e no PS2 `<ID sem _ e .>0000001`.

## Regras de arquitetura

1. **Caminhos:** use `AppPaths` e sempre `Path.Combine` (nada de `"\"` fixo: roda em Linux/macOS). Caminhos passados às ferramentas Sony (argumentos e entradas do GP4) passam por `Platform.ToolPath` (vira `Z:\...` no Wine). Caminhos *dentro* de ISOs continuam com `\`.
2. **UI fina:** os handlers só leem os controles, montam um `*BuildRequest` e exibem mensagens. Toda a lógica vai para `Shared/PSClassics.Core`, sem UI. Interação durante o build entra por callback (`ConfirmLibcrypt`, `Warn`); no Avalonia o build roda em `Task.Run` e os callbacks voltam para a thread de UI.
3. **Processos externos:** ferramentas `.exe` via `ToolRunner.Run` (decide entre exe direto e Wine); outros via `ProcessRunner.Run`. Nunca `cmd /c`, `sh -c` ou pipes.
4. **Sem ferramenta quando dá para fazer em código:** `strings`, `7z` e `crc` foram reimplementados (testes `[WindowsToolsFact]` comparam com os originais). Só gengp4, orbis-pub-cmd, cue2toc e pspdecrypt rodam via Wine.
5. **Imagens:** SkiaSharp (`ImageConverter`), nunca `System.Drawing` no Core.
6. **Streams:** sempre em `Using`, abrindo a origem antes de criar o arquivo de destino.
7. **Testabilidade:** lógica nova = função pura ou recebendo caminhos por parâmetro, com teste.
8. **Paridade das UIs:** WPF e Avalonia têm os mesmos campos, textos, dicas e ordem. Mudou um, mude o outro (e o `GuideWindow` dos dois).
9. **GP4:** editado por `Gp4Editor` (cabeçalho 1.1→1.0, `default_id` 1→0, entradas antes de `</files>`). Preserve esses ajustes.
10. **Interface:** WPF-UI 4.x no Windows; no Avalonia, `Styles.axaml` reproduz o visual (tema escuro, azul `#0070D1`, cards, `Expander.section`, `RadioButton.mode`). Os `x:Name` são usados pelo code-behind e pela automação de testes/screenshots: **não renomeie**. Depois de mexer em XAML, abra o app (ícones e estilos só falham em runtime).
11. **Textos visíveis:** escreva em inglês e traduza com `Localizer.T("...")` / `Localizer.F("... {0}", x)` (VB não tem escape de quebra de linha: escreva a barra-n literal dentro da string, o `T` converte). XAML fica em inglês: o `UiTranslator` de cada app traduz a janela (combo box items não são traduzidos: são valores). Todo texto novo precisa de uma linha em `Shared/PSClassics.Core/Localization/pt-BR.tsv`; o teste `TranslationCoverageTests` varre o XAML e o código dos dois apps e falha listando o que falta.
12. **Recursos para iniciantes** (2.2): modo passo a passo (`StepBar` + seções nomeadas `<Plataforma>GameSection`...), arrastar e soltar, buscar capa, predefinições, preflight, janela de resultado (`BuildResultWindow`), histórico, configurações e aviso de atualização existem nos dois apps com os mesmos nomes. Mudou um, mude o outro.

## Branches e fluxo de trabalho

- `main` é a versão publicada. Não há push direto: toda mudança entra por pull request.
- Nomes: `tipo/assunto-curto` em kebab-case e em inglês. Tipos: `feat/` (recurso), `fix/` (correção), `chore/` (manutenção, docs, CI), `release/` (preparação de versão).
- Uma branch = um assunto. Depois do merge, a branch é apagada (o GitHub apaga sozinho).
- Releases são tags anotadas `vX.Y.Z` na `main` (`v1`, `v2.0.0`, `v2.1.0`, `v2.2.0`).

## Pendências e riscos conhecidos

**Precisam de validação em console real**: qual fundo a tela inicial mostra (o app grava a imagem do usuário como `pic0.png`, mas os emuladores trazem um `pic1.png`); `-vu0-opt-flags` com um traço só (como no original, comentado em `EmulatorConfigs.vb`); `--lopnor-config=1` para configs PS3 do banco; `disc2.toc`..`disc4.toc` de PS1 multi-disco. Pacotes PSP sem ícone ficam sem `icon0.png`/`pic1.png`.

**Funções da 2.2.4 testadas só por código (sem clique real nem arquivo real):** a janela Fila e os botões de correção (WPF e Avalonia: só abrem e as listas são testadas); o suporte a `.cso` (testado com CSO feito no teste, não com um `.cso` de jogo PSP real); a fila com jogos de verdade pela internet (capa e título pelo Redump); os zips grandes (a extração roda em segundo plano mas não foi medida com 700 MB no Linux/macOS).

**Linux/macOS (não verificado em máquina real):** o pacote Linux e os testes com Wine rodam no CI; o macOS só compila no CI. No macOS, o Wine vem do Homebrew (`wine-stable`, via Rosetta no Apple Silicon) e as ferramentas `orbis-pub-cmd`/`gengp4` são 32 bits: confirmar que rodam. O `.app` é assinado ad-hoc (sem conta Apple), então o Gatekeeper pede "Abrir mesmo assim".

Notas: o `cue2toc` grava **dois** arquivos (`<bin>.TOC`, usado no pacote, e `<bin>.cu2`); roda numa pasta temporária. TIFF não é aceito (Skia não decodifica); WebP sim.

## Próximos passos

1. Testar os pacotes Linux e macOS em máquinas reais. 2. Validar em console real os itens acima. 3. Remover `legacy/` quando não for mais útil.
