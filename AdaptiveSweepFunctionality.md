# Funcionalidade de Sweep Adaptativo com Curvas Abertas no Plugin MorphMuse

Esta documentação descreve a implementação da funcionalidade de **Sweep (Varredura) adaptativo** no plugin MorphMuse, permitindo a geração de superfícies 3D a partir de duas polilinhas abertas: uma atuando como **trilho (rail)** e outra como **forma (profile)**. A forma é rotacionada para acompanhar a curvatura do trilho, criando uma superfície suave e contínua.

## 1. Conceito de Sweep Adaptativo

O Sweep adaptativo aqui implementado segue o princípio de varrer uma seção transversal (a curva **forma**) ao longo de um caminho (a curva **trilho**). A característica "adaptativa" reside na rotação da forma, que se alinha dinamicamente para ser perpendicular à tangente do trilho em cada ponto, garantindo uma transição suave e geometricamente correta da superfície.

## 2. Componentes Implementados

### 2.1. `SweepGenerator.cs` (Novo Serviço)

Este novo arquivo de serviço é o coração da lógica de Sweep. Ele contém o método estático `GenerateSweepContours`, que recebe a curva trilho e a curva forma (ambas como `List<Point3F>`) e retorna uma lista de contornos (`List<List<Point3F>>`) que representam as posições e orientações da forma ao longo do trilho.

**Detalhes da Implementação:**

*   **Normalização da Forma:** A curva forma é inicialmente normalizada, deslocando-a para que seu primeiro ponto coincida com a origem (0,0,0). Isso simplifica o posicionamento, pois a forma será "ancorada" ao trilho pelo seu ponto inicial.
*   **Iteração ao Longo do Trilho:** O algoritmo percorre cada ponto da curva trilho.
*   **Cálculo da Tangente:** Para cada ponto do trilho, a tangente é calculada usando o ponto atual e o próximo ponto (ou o anterior, se for o último ponto). Essa tangente define a direção "para frente" do trilho.
*   **Sistema de Coordenadas Local (Frenet-Serret Simplificado):** Um sistema de coordenadas local é criado em cada ponto do trilho:
    *   O eixo Z local é alinhado com a **tangente** do trilho.
    *   Um vetor auxiliar (`up`) é usado para calcular o eixo X local (perpendicular à tangente e ao vetor `up`).
    *   O eixo Y local é calculado como o produto vetorial da tangente e do eixo X local, completando a base ortonormal.
*   **Transformação da Forma:** Cada ponto da `normalizedProfile` é transformado para o sistema de coordenadas global usando a matriz de rotação e translação definida pelos eixos locais e pela posição atual no trilho. Isso garante que a forma seja posicionada e rotacionada corretamente.
*   **Contornos Gerados:** O resultado é uma série de `List<Point3F>`, onde cada lista representa a curva forma transformada em uma posição específica ao longo do trilho.

### 2.2. `MorphMuseController.cs` (Integração)

O `MorphMuseController.cs` foi atualizado para integrar o novo fluxo de Sweep:

*   **`Execute()`:** O método principal agora verifica a seleção do usuário. Se **duas polilinhas abertas** forem selecionadas, ele invoca o novo método `ExecuteSweepBetweenOpenCurves`.
*   **`ExecuteSweepBetweenOpenCurves(selectionManager)`:**
    *   **Identificação de Trilho e Forma:** Assume-se que a **primeira curva aberta selecionada** é o **trilho** e a **segunda** é a **forma**. (É importante que o usuário selecione na ordem correta ou inverta a seleção se o resultado não for o esperado).
    *   **Preparação das Curvas:** Ambas as polilinhas (trilho e forma) são convertidas para `List<Point3F>` e simplificadas usando o algoritmo Douglas-Peucker, similar ao processamento de outras curvas no plugin.
    *   **Geração dos Contornos de Sweep:** O método `SweepGenerator.GenerateSweepContours` é chamado com as curvas trilho e forma simplificadas para obter a série de contornos transformados.
    *   **Geração da Superfície Lateral:** A `SurfaceBuilderCopilot.GenerateLateralSurface` é então utilizada para criar a superfície 3D conectando esses contornos. O parâmetro `isClosed` é definido como `false`, pois a superfície resultante não terá tampas.
    *   **Adição ao CAD:** A superfície final é adicionada a uma nova camada (`MorphSweep`) no arquivo CAD.

## 3. Como Utilizar a Nova Funcionalidade

Para gerar uma superfície usando o Sweep adaptativo:

1.  No CamBam, selecione **exatamente duas polilinhas abertas**.
2.  A **primeira polilinha selecionada** será tratada como o **trilho**.
3.  A **segunda polilinha selecionada** será tratada como a **forma**.
4.  Execute o plugin MorphMuse.
5.  O plugin detectará a seleção e gerará a superfície 3D resultante do Sweep, com a forma rotacionando para seguir a curvatura do trilho.
