# CLAUDE.md

Guia para trabalhar neste repositório. Projeto open-source herdado (autor original: SvenGDK) e agora mantido neste fork. Itens marcados **(não verificado)** ainda não foram testados em build real.

## O que é

Gera **fPKGs** (pacotes "fake" para PS4/PS5) de jogos **PS1, PS2 e PSP**. Não é um emulador: é uma UI que prepara uma pasta de cache (emulador + `param.sfo` + configs + ícone/fundo), gera um projeto `.gp4` e chama `orbis-pub-cmd` para produzir o `.pkg`.

Três apps independentes, **sem código compartilhado**:

| | `Windows/` | `Linux/` | `macOS/` |
|---|---|---|---|
| Stack | VB.NET, WPF, **.NET 10** | C#, Avalonia 11, .NET 8 (a atualizar) | Swift 5, AppKit |
| Estado | migrado, refatorado e testado | ainda como o autor deixou | ainda como o autor deixou |
| Ferramentas | EXEs em `Tools\` | Wine + EXEs | scripts em Wine empacotado |

O trabalho de modernização está sendo feito primeiro no Windows; Linux e macOS devem receber depois as mesmas correções (ver "Paridade").

## Layout (Windows)

```
Windows/PS Classics fPKG Builder/            app WPF (projeto SDK-style, net10.0-windows)
  PSClassicsfPKGBuilder.xaml(.vb)            UI: lê os controles, monta um *BuildRequest e mostra resultados
  Core/                                      lógica sem UI (testável)
    PackageBuilders.vb                       PS1/PS2/PSP PackageBuilder + *BuildRequest (orquestra o build inteiro)
    EmulatorConfigs.vb                       texto dos config-title.txt / config-emu-ps4.txt (funções puras)
    SfoBuilder.vb  Gp4Editor.vb              param.sfo e edição do .gp4
    IsoTools.vb  PspTools.vb                 ISO/UDF (DiscUtils), bancos .dat, patch do EBOOT do PSP
    ProcessRunner.vb                         ProcessRunner (+ PkgTools: gengp4 / orbis-pub-cmd)
  PS1Game.vb PS2Game.vb                      IDs, títulos, libcrypt
  AppPaths.vb FileUtils.vb Utils.vb          caminhos, cópia de pastas, PNG
Windows/PS Classics fPKG Builder.Tests/      xUnit (C#): testes unitários + integração com as ferramentas reais
external/LibOrbisPkg/                        submódulo git (LGPL-3.0), compilado junto com o app
Tools/                                       NÃO versionada (.gitignore): ferramentas e emuladores de terceiros
```

`Tools/` vem do release original (`PS.Classics.fPKG.Builder.v1.Windows.7z`, pasta `Tools`). São binários proprietários/de terceiros: **nunca commitar** `Tools/`, `Cache/`, emuladores, DLLs ou ISOs/BINs.

## Comandos

Requer Windows e o .NET 10 SDK (Visual Studio é opcional).

```bash
git clone --recurse-submodules <repo>        # ou: git submodule update --init
dotnet build "Windows/PS Classics fPKG Builder.sln"
dotnet test  "Windows/PS Classics fPKG Builder.sln"
dotnet run --project "Windows/PS Classics fPKG Builder"
```

- O build copia `Tools/` (raiz do repo) para a pasta do executável quando ela existe. `-p:CopyToolsToOutput=false` desliga isso (build mais rápido, útil para rodar só testes unitários).
- Os testes de integração (`ToolsIntegrationTests`, `[RequiresToolsFact]`) rodam as ferramentas reais e geram pacotes PS1/PS2/PSP a partir de discos sintéticos; sem `Tools/` eles são **ignorados**, não falham.
- Os testes usam `TempWorkspace`: apontam `AppPaths.BaseDirectory` para uma pasta temporária (com junction para `Tools/`), então o repositório nunca é alterado. Testes não rodam em paralelo (estado estático).
- macOS **(não verificado)**: `xcodebuild -project "macOS/fPKG Builder.xcodeproj" -scheme "fPKG Builder" build` (requer Mac + Xcode 15+). Linux: `dotnet build Linux/makePSClassicsfPKG.sln`.

## Convenções de nomenclatura (como o código já é; manter)

**VB.NET**
- `PascalCase` para tudo, inclusive locais e parâmetros (`GameCacheDirectory`, `PKGOutputFolder`). Instâncias locais descartáveis começam com `New` (`NewProcess`); `OFD`/`FBD` para os diálogos.
- Controles XAML: `<Plataforma><Função><Tipo>` (`PS1TitleTextBox`, `BrowsePS1Disc1Button`); handlers `<Controle>_Click`. Na aba PS2 alguns controles não têm prefixo (`SelectedDisc1TextBox`); em controles novos **use sempre o prefixo da plataforma**.
- Classes de Core: substantivos (`Gp4Editor`, `SfoBuilder`), métodos `Shared`; opções em classes `*Options`/`*BuildRequest`.
- Comentários em inglês, com aspa simples (`'Create a TOC file`) e `'''` para XML docs. Strings de UI em inglês.
- Config do emulador: linhas `--chave=valor`; comentários `#Texto`.

**Swift (macOS)**: também `PascalCase` (`@IBOutlet weak var GameTitleTextField`, `@IBAction func BrowseDisc1`), comentários `// Texto`.

**Testes (C#)**: um arquivo por área, `NomeDaClasseTests`, métodos `Metodo_ComportamentoEsperado`; bugs corrigidos ganham um teste de regressão com comentário `// Regression: ...`.

**Domínio**
- "NP Title" = ID de 9 caracteres (`SLUS00594`, sem `_`/`.`), validado com `Length <> 9`.
- Cache: `Cache\PS1fPKG`, `PS2fPKG`, `PSPfPKG` + `<nome>.gp4` ao lado. Discos no PKG: `data/disc1.bin` (PS1), `image/disc01.iso` (PS2), `data/USER_L0.IMG` (PSP).
- `CONTENT_ID` = `UP9000-<NP>_00-<sufixo>`: `<NP>PS1FPKG`, `<NP>PSPFPKG`, e no PS2 `<ID sem _ e .>0000001` no Windows (o macOS usa `<ID>PS2FPKG`: inconsistência entre os apps).

## Regras de arquitetura

1. **Caminhos:** use `AppPaths` (`BaseDirectory`, `ToolsDirectory`, `CacheDirectory`). Nunca `CurrentDirectory`. `strings.exe` etc. são chamados pelo caminho completo em `Tools`.
2. **UI fina:** os handlers só leem os controles, montam um `*BuildRequest` e exibem mensagens. Toda a lógica vai para `Core/`, sem `MsgBox`/WPF. Interação com o usuário durante o build entra por callback no request (`ConfirmLibcrypt`, `Warn`).
3. **Processos externos:** use `ProcessRunner.Run` (lê stdout/stderr antes de `WaitForExit`; sem janela de console). Nunca `cmd /c ... | findstr`.
4. **Streams:** sempre em `Using`, abrindo a origem antes de criar o arquivo de destino.
5. **Testabilidade:** lógica nova = função pura ou recebendo caminhos por parâmetro, com teste. Não use `My.Computer` (não existe em WPF no .NET moderno; use `FileUtils`).
6. **Paridade entre plataformas:** PS1/PS2/PSP e Windows/Linux/macOS repetem a mesma lógica. Ao corrigir um bug, procure o mesmo padrão nas outras cópias.
7. **GP4:** editado por `Gp4Editor` (cabeçalho 1.1→1.0, `default_id` 1→0, entradas antes de `</files>`). Preserve esses ajustes.
8. **macOS:** nunca force-unwrap `URL(string:)` com caminho de arquivo (use `URL(fileURLWithPath:)`); não concatene entrada do usuário em `sh -c`; não bloqueie a thread principal com `sleep`.

## Pendências e riscos conhecidos

Corrigidos no Windows (com testes): disco 3/4 do PS1, aspas de `--path-patches`, fixes do PS2 perdidos, `IndexOutOfRange` em `GetPS2GameID` (inclusive o caminho `Volume:` que nunca funcionava), deadlock de stdout, handles vazados, dependência do diretório de trabalho, `strings.exe` via `cmd`, `--lopnor-config=1` ausente quando a config PS3 vem do `.dat` (o caso comum), arquivo temporário do PSP, `Insert` do libcrypt com ID curto.

**Precisam de validação em console real** (não dá para testar aqui): `-vu0-opt-flags` com um traço só (mantido como no original, comentado em `EmulatorConfigs.vb`) e o `--lopnor-config=1` agora ativo para configs PS3 do banco de dados.

Abertos no Windows: PS2 com ID não detectado ("usar mesmo assim") gera `CONTENT_ID` e caminhos inválidos; `cue2toc`/`.toc` só para o disco 1 no PS1 (e o `.toc` é gerado na pasta do jogo do usuário); mensagem modal "All files ready" foi removida (o build segue direto).

macOS: crash com `URL(string:)!` em caminhos com espaço; config do PS2 escrita sem `\n` e com a lógica de "reiniciar emulador" invertida; fundo copiado do campo do ícone (PS2 e PSP); disco omitido do GP4 se o ISO já existe em `/Games`; `BrowseMemoryCard` é um stub vazio; `ShaderComboBox`/`LUAConfigTextField` nunca são lidos; sem paridade de features do PS2 com o Windows; `launchPath`/`launch()` deprecados.

Linux: ainda não analisado a fundo (C#, ~2,3 mil linhas, provavelmente com os mesmos bugs do Windows original); .NET 8 já está sem suporte.

## Próximos passos

1. Linux: atualizar para .NET 10, aplicar as correções do Windows, criar testes. 2. macOS: corrigir os crashes e a config PS2. 3. CI (GitHub Actions, `windows-latest`, `dotnet test`).
