# OverlayAll

Overlay preto semitransparente sobre toda a área de trabalho do Windows (todos os monitores),
sem bordas, sempre no topo e click-through: todos os apps continuam usáveis "atrás" dele.
Feito para reduzir o brilho de telas brancas (fotofobia).

## Build

Sem dependências — usa o `csc.exe` do .NET Framework 4 que já vem no Windows.

- No Windows: execute `build.bat`
- No WSL: `/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /optimize+ /out:OverlayAll.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll OverlayAll.cs`

## Uso

Execute `OverlayAll.exe`. Ao abrir, uma janelinha no centro da tela mostra os controles; clique **OK** (ou Enter/Esc) e o overlay continua ativo. Ela reaparece pelo item "Mostrar controles" da bandeja e não aparece quando iniciado com o Windows. Um botão escuro com "?" fica grudado na lateral direita da tela: clique para reabrir esse modal; segure e arraste para movê-lo para qualquer lugar (a posição é lembrada). Ele some junto com o overlay quando desligado. Controle pelo ícone na bandeja (escudo) ou pelos atalhos:

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+O` | Liga/desliga overlay (ou duplo clique no ícone) |
| `Ctrl+Alt+↑` / `Ctrl+Alt+↓` | Opacidade +5% / -5% (5–90%) |
| `Ctrl+Alt+Q` | Sair |

Menu da bandeja: presets de opacidade, "Ocultar em prints e compartilhamento de tela"
(ligado por padrão — prints e reuniões saem sem escurecimento), "Iniciar com o Windows".

Configuração salva em `%APPDATA%\OverlayAll\settings.ini`.

> Para "Iniciar com o Windows", copie o `.exe` para uma pasta do Windows
> (ex: `%LOCALAPPDATA%\OverlayAll\`) antes de ativar — caminho `\\wsl.localhost\...`
> depende do WSL estar rodando.

## Limitações

- Não cobre a tela de UAC/tela de bloqueio (desktop seguro) nem jogos em tela cheia exclusiva.
- Alguns elementos do shell (menu Iniciar, central de notificações) podem aparecer por cima.
- Escurece por igual (inclusive áreas já escuras); para telas brancas específicas, combine com modo escuro nos apps.
