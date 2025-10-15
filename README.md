# DafTools

Ferramenta console (parte de um projeto WPF) para baixar, processar e consolidar dados de demonstrativos (DAF) e indicadores de PIB por município. Escrita em C# 12 targeting .NET 8.

## Sumário
* [Visão geral](#visão-geral)
* [Arquitetura / classes principais](#arquitetura--classes-principais)
* [Fluxos principais (o que acontece e por quê)](#fluxos-principais-detalhado--o-que-como-e-por-quê)
* [Arquivos e recursos esperados](#arquivos-e-recursos-esperados)
* [Como executar](#como-executar)
* [Pontos de atenção e sugestões de manutenção](#pontos-de-atenção-e-sugestões-de-manutenção-prioritárias)

---

## Visão geral
`DafTools` automatiza:
* Consulta e download de DAFs mensais (API do DAF do banco).
* Extração de indicadores do PIB (páginas IBGE).
* Armazenamento local em JSON.
* Exportação consolidada para CSV.

**Objetivo:** facilitar a coleta periódica e a geração de relatórios consolidados por município.

---

## Arquitetura / classes principais

* **`Program.cs`**
    * Ponto de entrada (console loop).
    * Mostra o menu, recebe a opção do usuário e aciona os serviços.
    * **Fluxos**: baixar DAFs, exportar CSVs, baixar PIB, exportar PIB, gerenciar cidade interna.
    > **Observação**: usa instâncias concretas (`new RequestService()`, `new ExportService()`, `new CitiesService()`).

* **`Services/CitiesService.cs`**
    * **Responsabilidade**: gerenciar o arquivo local `Resources/municipios.json` com a base interna de municípios.
    * **Propriedades e métodos**:
        * `CitiesInfo`: coleção carregada de `CityInfoResult`.
        * `LoadCities()`: lê e desserializa `municipios.json`.
        * `ListAllCities()`: imprime todas as cidades carregadas.
        * `SaveCitiesFile()`: serializa e grava `municipios.json`.
        * `AddNewCity(CityInfoResult)`: adiciona cidade em memória e persiste.
        * `Teste()`: utilitário que parseia HTML (arquivo local) e gera `lista_codigos_pib.json` — usado para construir o mapeamento NF/UF → código PIB.
    * **Por quê**: centraliza a persistência de municípios e facilita a adição manual via menu.

* **`Services/RequestService.cs`**
    * **Responsabilidade**: comunicação HTTP com APIs e scraping de HTML do IBGE.
    * **Principais métodos**:
        * `RequestDafsData()`: para cada cidade registrada em `municipios.json`, executa chamadas `POST` à API DAF e salva o JSON por mês em uma pasta determinada pelo usuário.
            * Gera payload: `codigoBeneficiario` (`DafCode`), `dataInicio`/`dataFim`.
            * Salva arquivos em pastas por cidade (nome com underscores).
        * `RequestPibData()`: para cada cidade, realiza `GET` ao endpoint do IBGE (`url` + `PibCode`), faz parsing via regex e monta um dicionário por cidade/ano/indicador, salvando em `indicadores_pib.json`.
        * `RequestCityDafCode(string cityName)`: busca possíveis beneficiários na API DAF para permitir ao usuário escolher a correspondência correta. Retorna o `CityInfoResult` selecionado.
        * `RequestCityPibCode(CityInfoResult)`: lê `Resources/lista_codigos_pib.json` e retorna o código IBGE correspondente.
    * **Por quê**: automatizar a coleta e o mapeamento de códigos externos (DAF/IBGE) e salvar os dados brutos localmente para processamentos posteriores.

* **`Services/ExportService.cs`**
    * **Responsabilidade**: consolidar os JSONs gerados em arquivos CSV para análise/relatórios.
    * **Métodos**:
        * `ExportDafsCsv()`: percorre as pastas de DAFs geradas, parseia os JSONs de cada mês, extrai fundos, débitos e créditos e gera um CSV consolidado com as colunas: `Prefeitura;Ano;Mês;Fundo;Débito;Crédito`.
        * `ExportPibsCsv()`: lê `indicadores_pib.json` e gera um CSV com `Cidade;Ano;Indice;Valor` (faz o mapeamento para o nome padrão quando possível).
    * **Por quê**: produzir um artefato tabular que facilita a ingestão em ferramentas como Excel ou BI.

* **`Model/DafCsvScheme.cs`**
    * DTO simples usado para montar as linhas do CSV de DAF: `Name`, `Year`, `Month`, `Fund`, `Debt`, `Credit`.

* **`Model/CityInfoResult` (record)**
    * **Estrutura**: `(Name, Uf, DafCode)` + propriedade `PibCode`.
    * **Uso**: representa um município na base interna e carrega ambos os códigos (DAF e PIB).

* **`Utils/PathUtils`** (não mostrado no código)
    * **Função**: selecionar o diretório de destino via input do usuário (diálogo); centraliza a lógica de escolha de caminhos.

---

## Fluxos principais (detalhado — o que, como e por quê)

1.  **Adicionar município (menu → opção 5)**
    * Usuário informa o nome da cidade.
    * `RequestService.RequestCityDafCode` consulta a API do DAF; o usuário escolhe entre as correspondências retornadas.
    * `RequestService.RequestCityPibCode` associa o `PibCode` lendo `Resources/lista_codigos_pib.json`.
    * `CitiesService.AddNewCity` persiste o resultado em `Resources/municipios.json`.
    * **Por quê**: garantir que cada cidade na base de dados tenha os códigos necessários para baixar DAFs e indicadores do PIB.

2.  **Baixar DAFs (menu → opção 1)**
    * Para cada município em `CitiesService.LoadCities()`:
        * Itera de Janeiro de 2023 até a data atual, mês a mês.
        * Constrói o payload e envia uma requisição `POST` para a API DAF.
        * Salva o JSON formatado por cidade/ano-mês em uma pasta escolhida pelo usuário.
    * **Por quê**: armazenar arquivos brutos por mês para posterior análise e exportação.

3.  **Baixar PIBs (menu → opção 3)**
    * Para cada município:
        * Faz uma requisição `GET` ao endpoint do IBGE com o código do município.
        * Usa regex para extrair os indicadores (label/value) por ano.
        * Monta uma estrutura hierárquica `cidade → ano → indicador` e salva em `indicadores_pib.json`.
    * **Por quê**: coletar indicadores econômicos por município em um formato estruturado.

4.  **Exportar CSVs (menu → opções 2 e 4)**
    * `ExportDafsCsv`: varre as pastas de DAF, extrai fundos e valores (débitos/créditos) e consolida tudo em um único arquivo CSV.
    * `ExportPibsCsv`: converte o arquivo `indicadores_pib.json` em um CSV tabular.
    * **Por quê**: produzir relatórios consumíveis por ferramentas de BI ou Excel.

---

## Arquivos e recursos esperados

* `Resources/municipios.json` — base interna de municípios (`CityInfoResult[]`).
* `Resources/lista_codigos_pib.json` — mapeamento `"nome (UF)"` → `codigo IBGE` (gerado por `CitiesService.Teste` ou por coleta manual).
* Pastas/diretórios onde os DAFs são salvos (estrutura: `<targetPath>/<NomeCidade_underscores>/<YYYY-MM>.json`).
* `indicadores_pib.json` (gerado por `RequestPibData`).
> **Observação**: os arquivos JSON devem usar codificação UTF-8.

#### Formato esperado (exemplo) de entrada para cidades (`municipios.json`):
```json
[
    {
        "Name": "santa rita",
        "Uf": "SP",
        "DafCode": 12345,
        "PibCode": 4205175
    }
]
```
*(Na prática, o app serializa uma lista de `CityInfoResult`)*

---

## Como executar

### Requisitos:
* .NET 8 SDK
* Visual Studio 2022 ou `dotnet` CLI

### Passos:
1.  Abrir o projeto no Visual Studio 2022 ou abrir um terminal no diretório do projeto.
2.  Restaurar os pacotes (se houver).
3.  Executar com `dotnet run` (ou através do debugger do VS).
4.  Seguir o menu para escolher as opções:
    * Adicionar os municípios necessários antes de baixar os dados de DAFs/PIBs.
    * Ao baixar, será solicitado selecionar uma pasta de destino (`PathUtils`) — forneça um diretório válido.

---

## Pontos de atenção e sugestões de manutenção (prioritárias)
*(Esta seção pode ser preenchida com futuros TODOs, bugs conhecidos ou melhorias planejadas)*
