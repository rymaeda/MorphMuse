# CLAUDE.md — MorphMuse: Contexto de Desenvolvimento de Plugin CamBam

Este documento fornece o contexto completo para trabalhar com este projeto — um plugin para o software CAM **CamBam**. Ele documenta as particularidades da API do CamBam, as estruturas de dados usadas, os padrões arquiteturais adotados e as convenções específicas do domínio (geometria 3D, malhas de superfície, manipulação de CAD).

---

## 1. Visão Geral do Projeto

**MorphMuse** é um plugin para o CamBam que gera superfícies 3D (malhas triangulares) a partir de curvas 2D/3D selecionadas pelo usuário no ambiente CAD. O plugin suporta dois fluxos principais:

1. **Curva fechada + Curva aberta (geratriz)**: Gera uma superfície de revolução/perfil ao redor de uma base fechada, guiada por uma curva aberta que define a "geratriz" (perfil de variação ao longo da altura).
2. **Duas curvas abertas**: Gera uma superfície tipo "sweep"/loft entre um trilho (rail) e uma forma (form).

- **Target Framework**: .NET Framework 4.8
- **Linguagem**: C# 7.3
- **Tipo de projeto**: Class Library carregada dinamicamente pelo CamBam como plugin
- **Ponto de entrada**: `Program.cs` → método estático `InitPlugin(CamBamUI ui)`

---

## 2. Estrutura do Projeto

### 2.1 Diretórios e Arquivos Principais
- `MorphMuse.csproj`: Arquivo do projeto.
- `Plugin.cs`: Classe principal do plugin, contém metadados e métodos de inicialização.
- `MainForm.cs`: Formulário principal da interface do usuário.
- `Settings.cs`: Classe para gerenciamento de configurações do usuário.

### 2.2 Classes e suas Funções
- `Plugin`: Ponto de entrada do CamBam que carrega o plugin.
- `MainForm`: Classe que define o formulário principal do usuário.
- `Settings`: Classe que gerencia as configurações salvas do usuário.

## 3. Particularidades da API do CamBam

### 3.1 Carregamento do Plugin
O CamBam carrega plugins na inicialização através de reflexão. O método `InitPlugin` na classe `Plugin` é o ponto de entrada onde você pode adicionar seus manipuladores de menu, comandos e inicializar recursos.
 
### 3.2 Manipulação de Entidades CAD
Entidades como `Line`, `Arc`, `Circle`, `Polyline`, etc., são manipuladas através de suas propriamente ditas classes C#. Por exemplo, `CamBam.CAD.Line`para linhas. Cada entidade tem uma coleção de pontos (`Point3FArray`) e métodos para transformação e manipulação geométrica.

**Gotcha**: Não existem métodos estáticos de conveniência como `Surface.CreateExtrusion(...)` na API do CamBam. Toda superfície deve ser construída manualmente populando `Points` (`Point3FArray`) e `Faces` (`TriangleFace[]`).

### 4.5 `Polyline` (Entity, `CamBam.CAD`)
- `.Points` → `List<PolylineItem>` (cada item tem `.Point` de tipo `Point3F` e pode representar arco via bulge).
- `.Closed` → bool.
- `.RemoveArcs(tolerance)` → retorna nova `Polyline` linearizada.
- `.CreateOffsetPolyline(offset, tolerance)` → retorna `Polyline[]` (pode gerar múltiplos contornos ou `null`/vazio se o offset for inválido).
- `.ToPrimitives()` → decompõe em `Entity[]` (Lines/Arcs).
- `.GetExtrema(ref min, ref max)` / `.GetExtents(ref min, ref max)` → bounding box.
- `.ApplyTransformation()` / `.Transform` (`Matrix4x4F`) — **gotcha**: sempre clone (`(Polyline)curve.Clone()`) antes de aplicar transformação para não mutar a entidade original do CAD.

### 4.6 `Layer` / `LayerCollection` (`CamBam.CAD`)

**Gotcha crítico de Undo**: Não existe um método nativo tipo `UndoBuffer.AddLayerCreation()`. Criar um layer novo **não é automaticamente desfeito** pelo Ctrl+Z. A estratégia usada neste projeto é:
1. Registrar apenas a **entidade criada** (`UndoBuffer.Add(surfaceEntity)`) — não a coleção de entidades do layer inteiro (isso causa desfazer cascata incorreto, removendo entidades não relacionadas em Ctrl+Z sucessivos).
2. Implementar uma rotina de **cleanup manual** (`CleanupEmptyMorphLayers`) que remove, na próxima execução do plugin, quaisquer layers vazios com o prefixo usado (ex: `"MorphSurface"`), compensando a ausência de undo nativo para criação de layer.

### 4.7 `CADFile` (`CamBam.CAD`)
Documento CAD ativo, acessível via `_ui.ActiveView.CADFile`. Métodos/propriedades relevantes:

### 4.8 `CamBamUI` / `ICADView`

### 4.9 `UndoBuffer` (`CamBam.Util.UndoBuffer`)
Métodos disponíveis (obtidos via reflection/disassembly):

**Padrão recomendado**:
	
---

## 5. Padrões de Log e Diagnóstico

Todo log de diagnóstico do plugin usa:

Isso escreve na janela de Output do CamBam. **Limitação conhecida**: mensagens de log não podem ser copiadas pelo usuário diretamente da UI do CamBam. Para depuração que precise ser copiada/exportada, escreva a informação na propriedade `.Tag` de uma entidade (`Surface.Tag`, por exemplo), que pode ser inspecionada e copiada no painel de propriedades do CamBam.

---

## 6. Algoritmos Centrais do Domínio

### 6.1 Douglas-Peucker (`PolylineSimplifier.cs`)
Reduz o número de pontos de uma polilinha mantendo a forma dentro de uma tolerância. Usa distância perpendicular via produto vetorial (`Geometry3F.Cross`).

### 6.2 Ear Clipping (`EarClippingTriangulator.cs`)
Triangula polígonos **côncavos** (usado para gerar a tampa/"cap" superior da superfície). Pontos importantes:
- Deve projetar pontos 3D para um plano 2D dominante (baseado na normal do polígono, calculada via método de Newell) antes de aplicar testes de convexidade/inclusão.
- Determina orientação (horário/anti-horário) via fórmula do Shoelace.
- Teste ponto-em-triângulo via coordenadas baricêntricas com epsilon de tolerância numérica.
- **Gotcha**: se a curva de entrada já estiver fechada (primeiro ponto == último ponto), remova o ponto duplicado antes de triangular, senão o algoritmo falha ou gera faces degeneradas.

### 6.3 Construção de Superfície Lateral (`SurfaceBuilderCopilot.cs`)
Estratégia de "loft"/"strip" entre pares de curvas consecutivas (`lower`/`upper`):
- Sincroniza rotação da curva superior em relação à inferior (`FindClosestIndex` + `RotateCurve`), evitando "torção" na malha quando as curvas são fechadas.
- Triangulação adaptativa: em cada passo, compara a distância entre `lower[i+1]↔upper[j]` vs `lower[i]↔upper[j+1]` e escolhe a menor diagonal para formar o triângulo — produzindo uma malha mais suave e sem auto-interseção quando as curvas têm densidades de pontos diferentes.
- Após o loop principal, consome os pontos restantes da curva mais longa.
- Orientação fixa dos triângulos (`ia, ic, ib`) para manter normais consistentes (voltadas para fora).
- Descarte de faces degeneradas via produto vetorial (área ≈ 0).

### 6.4 Sweep Generator (`SweepGenerator.cs`)
Gera contornos transversais posicionando e rotacionando um perfil ("profile") ao longo de um trilho ("rail"), usando uma base ortonormal simplificada (Frenet-Serret):
- Tangente calculada por diferença entre pontos consecutivos do rail, normalizada manualmente (construa um novo vetor dividido pelo comprimento — `Vector3F` não expõe normalização in-place neste contexto de uso).
- Vetor `worldUp` auxiliar (0,0,1), trocado para (1,0,0) quando quase paralelo à tangente (evita singularidade no produto vetorial).
- Eixos locais X (normal) e Y (binormal) via produtos vetoriais cruzados, com fallback para eixos padrão em caso de vetor nulo.
- Aplica rotação fixa de 90° no perfil antes de mapear para o espaço do trilho.

### 6.5 Amostragem de Curvas (`CurveSampler.cs`)
Converte segmentos de `Polyline` (via `ToPrimitives()`) em pontos discretos:
- Arcos: número de pontos proporcional ao comprimento do arco (`raio * sweep_rad`) dividido pelo passo máximo (`StepMax`).
- Linhas: apenas os dois extremos são adicionados.
- Remove ponto duplicado de fechamento via `Point3F.Match(...)`.

---

## 7. Fluxo de Execução (`MorphMuseController.cs`)

### Etapas de preparação de curvas
1. **`SettingsManager.GetSmartAdaptiveParameters`**: calcula tolerância de simplificação e passo de amostragem com base no tamanho da bounding box da curva-guia (diagonal), normalizado contra `CamBamConfig.Defaults.STEPResolution`.
2. **`OpenPolylineProcessor`**: remove arcos, normaliza origem (translada para (0,0,0)) e aplica Douglas-Peucker na curva geratriz/aberta.
3. **`LayerGenerator`**: gera contornos paralelos (offset) para cada ponto de referência da geratriz — cada offset representa uma "fatia" em uma altura Z diferente (`pt.Y` da geratriz vira `Z` do contorno).
4. **`CurveSampler`**: amostra pontos ao longo de cada contorno com densidade adaptativa baseada no espaçamento entre pontos da geratriz.
5. **`PolylineSimplifier`**: simplifica novamente cada curva amostrada.
6. **`SurfaceBuilderCopilot`**: gera a malha final (lateral + cap opcional).

---

## 8. Conversão de Unidades (`SettingsManager.cs`)

O CamBam trabalha com múltiplas unidades de desenho (`Millimeters`, `Inches`, `Centimeters`, `Meters`, `Thousandths`). Como os parâmetros internos do plugin são calculados em milímetros, sempre converta:

`GetUnits()` lê `CamBamUI.MainUI.ActiveView.CADFile.DrawingUnits` com fallback seguro (`Units.Unknown`) em `try/catch`.

---

## 9. Convenções e Particularidades do Plugin CamBam

1. **Ciclo de vida**: O plugin não tem `Main()`. O CamBam invoca `InitPlugin(CamBamUI ui)` estaticamente ao carregar a DLL da pasta de plugins. Registre menus/eventos aqui.
2. **Threading**: Todas as operações ocorrem na thread de UI do CamBam (WinForms). Não há necessidade de `Invoke`/`BeginInvoke` a menos que se use threads em background manualmente.
3. **Clonagem de entidades**: Sempre clone (`.Clone()`) entidades do CAD antes de aplicar transformações para não corromper o desenho original acidentalmente.
4. **Nomes de Layer únicos**: Use um padrão de sufixo numérico (`{baseName}{index:D3}`) e verifique com `cadFile.HasLayer(...)` antes de criar.
5. **Undo é manual e granular**: Sempre `AddUndoPoint` antes de qualquer mutação; registre apenas os objetos estritamente necessários (evite registrar coleções inteiras para não causar "undo em cascata" desfazendo entidades não relacionadas).
6. **`OnModified()` é obrigatório**: Sem essa chamada, o CamBam pode não sincronizar corretamente o estado do documento com o undo/redo e a UI (título com asterisco `*` de "modificado").
7. **Idioma dos comentários**: Todos os comentários de código devem estar em **inglês**, independentemente do idioma da comunicação com o desenvolvedor.
8. **Namespace CamBam vs MorphMuse**: Tome cuidado com `using CamBam;` (namespace raiz, contém `ThisApplication`) vs `using CamBam.CAD;` / `using CamBam.Geom;` — são assemblies/namespaces distintos.
9. **Sem métodos de conveniência na API**: A API do CamBam não fornece atalhos como extrusão automática de curvas em superfícies — toda malha deve ser construída manualmente (vértices + faces).

---

## 10. Notas sobre Performance

- **Evitando cópias desnecessárias**: Ao trabalhar com grandes conjuntos de pontos, evite duplicar `List<Point3F>` sem necessidade. Prefira passar referências e mutar apenas quando explicitamente indicado (ex: `Clear()` + `AddRange()` em `AlignCurveToPrevious`).
- **Deduplicação via dicionário**: O uso de `Dictionary<Point3F, int>` para deduplicação de vértices evita entidades de superfície infladas com pontos repetidos — sempre use `Geometry3F.AddPoint(...)` em vez de inserir diretamente em `Point3FArray`.
- **Limite de iterações em algoritmos geométricos**: Tanto o Ear Clipping quanto o Douglas-Peucker devem ter guardrails de iteração (ex: `iterations < 2000` ou baseado em `n²`) para evitar loops infinitos em entradas degeneradas.

---

## 11. Referências Rápidas de Assinaturas

---

## 12. Checklist para Novas Features

Ao adicionar uma nova funcionalidade de geração de superfície neste plugin, verifique:

- [ ] As curvas de entrada foram normalizadas/simplificadas antes da triangulação?
- [ ] A orientação dos triângulos está consistente com o restante da malha (normais para fora)?
- [ ] Vértices duplicados são deduplicados via `Dictionary<Point3F, int>`?
- [ ] Faces degeneradas (área ≈ 0) são descartadas?
- [ ] `AddUndoPoint` é chamado **antes** de qualquer mutação no `CADFile`?
- [ ] Apenas as entidades estritamente novas são registradas no `UndoBuffer`?
- [ ] `cadFile.OnModified()` é chamado ao final da operação?
- [ ] Layers órfãos/vazios de execuções anteriores são limpos no início da execução?
- [ ] Conversões de unidade usam `SettingsManager.ConvertFromMillimeters`/`ConvertToMillimeters`?
- [ ] Comentários de código estão em inglês?

---

## 13. Links de Referência Externos

- **Site do CamBam**: https://www.cambam.info/
- **Repositório deste projeto**: https://github.com/rymaeda/MorphMuse