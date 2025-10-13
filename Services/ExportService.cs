using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DafTools.Model;
using DafTools.Utils;

namespace DafTools.Services
{
    public class ExportService
    {
        PathUtils _pathContext = new PathUtils();
        CitiesService _citiesService = new CitiesService();
        public void ExportDafsCsv()
        {
            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Selecione a pasta onde estão os DAFs:");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para abrir a seleção");
            Console.ReadKey();

            string DataPath = _pathContext.GetPathByInput();

            string[] citiesPaths = Directory.GetDirectories(DataPath);

            List<DafCsvScheme> dafSchemes = new();

            var citiesInfo = _citiesService.LoadCities();

            foreach (string city in citiesPaths)
            {
                string cityFolderName = Path.GetFileName(city);

                // tenta achar cidade correspondente
                var cityInfo = citiesInfo.FirstOrDefault(c =>
                    cityFolderName.Equals(c.Name.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase));

                string displayName = cityInfo != null
                    ? $"{cityInfo.Name} ({cityInfo.Uf})"
                    : cityFolderName; // fallback se não encontrar


                string[] cityJsonFiles = Directory.GetFiles(city, "*.json");

                foreach (string jsonFile in cityJsonFiles)
                {
                    string jsonFileName = Path.GetFileNameWithoutExtension(jsonFile);

                    var fileDate = jsonFileName.Split('-');
                    int year = int.Parse(fileDate[0]);
                    int month = int.Parse(fileDate[1]);

                    // carregar JSON
                    string content = File.ReadAllText(jsonFile);
                    using var doc = JsonDocument.Parse(content);
                    var fundsObject = doc.RootElement.GetProperty("quantidadeOcorrencia");

                    var linhas = fundsObject.EnumerateArray()
                    .Select(x => x.GetProperty("nomeBeneficio").GetString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                    string currentFund = null;
                    bool waitingDebt = false;
                    bool waitingCredit = false;
                    bool byPassFirst = true;


                    foreach (var linha in linhas)
                    {

                        if (byPassFirst)
                        {
                            byPassFirst = false;
                            continue; // pula a primeira linha (cidade)
                        }

                        // Passo 1: detectar fundo
                        if (currentFund == null && Regex.IsMatch(linha, @"^[A-Z]{2,}\s+-"))
                        {
                            currentFund = linha;
                            waitingDebt = true;
                            waitingCredit = true;
                        }

                        // Passo 2: capturar débito
                        if (waitingDebt && linha.Contains("DEBITO FUNDO") && currentFund != null)
                        {
                            var sections = linha.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var debtValue = double.Parse(sections.Last().Remove(sections.Last().Count() - 1)).ToString("F2", new CultureInfo("pt-br"));

                            dafSchemes.Add(new DafCsvScheme
                            {
                                Name = displayName,
                                Year = year,
                                Month = month,
                                Fund = currentFund,
                                Debt = debtValue,
                                Credit = "" // será preenchido depois
                            });

                            waitingDebt = false; // já achou débito desse fundo
                        }

                        // Passo 3: capturar crédito
                        if (waitingCredit && linha.Contains("CREDITO FUNDO") && currentFund != null)
                        {
                            var sections = linha.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var creditValue = double.Parse(sections.Last().Remove(sections.Last().Count() - 1)).ToString("F2", new CultureInfo("pt-br"));

                            var lastScheme = dafSchemes.LastOrDefault(r =>
                                r.Name == displayName &&
                                r.Year == year &&
                                r.Month == month &&
                                r.Fund == currentFund);

                            if (lastScheme != null)
                                lastScheme.Credit = creditValue;
                            else
                                dafSchemes.Add(new DafCsvScheme
                                {
                                    Name = displayName,
                                    Year = year,
                                    Month = month,
                                    Fund = currentFund,
                                    Debt = "",
                                    Credit = creditValue
                                });

                            waitingCredit = false;

                            currentFund = null;
                        }
                    }

                    Console.WriteLine($"{cityFolderName} {year}-{month:D2} processado.");
                }
            }

            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Agora voce deve selecionar a pasta onde o arquivo CSV será exportado");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para continuar");
            Console.ReadKey();


            string exportPath = Path.Combine(_pathContext.GetPathByInput(), $"{DateTime.Now.Month}-{DateTime.Now.Day}-{DateTime.Now.Hour}-{DateTime.Now.Minute}_consolidado_daf.csv");
            try
            {
                using (var sw = new StreamWriter(exportPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("Prefeitura;Ano;Mês;Fundo;Débito;Crédito");
                    foreach (var r in dafSchemes)
                    {
                        sw.WriteLine($"{r.Name};{r.Year};{r.Month:D2};{r.Fund};{r.Debt};{r.Credit}");
                    }
                }

                Console.WriteLine("-------------------------------------");
                Console.WriteLine();
                Console.WriteLine($"Relatório consolidado salvo em {exportPath}");
                Console.WriteLine();

            }
            catch (Exception ex)
            {
                Console.WriteLine("-------------------------------------");
                Console.WriteLine("Erro ao criar arquivo, mesangem de erro abaixo");
                Console.WriteLine();
                Console.WriteLine(ex.Message);
            }
        }

        public void ExportPibsCsv()
        {
            IList<PibCsvScheme> pibSchemes = new List<PibCsvScheme>();


            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Selecione a pasta onde está o arquivo do PIB:");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para abrir a seleção");
            Console.ReadKey();

            string DataPath = _pathContext.GetPathByInput();

            string json = File.ReadAllText(Path.Combine(DataPath, "indicadores_pib.json"));

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var citiesPibInfo = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json, options);

            var citiesInfo = _citiesService.LoadCities();

            foreach (var cityContext in citiesPibInfo)
            {
                string cityName = cityContext.Key;

                // tenta achar cidade correspondente
                var cityInfo = citiesInfo.FirstOrDefault(c => cityName.Equals(c.Name));

                string displayName = cityInfo != null
                    ? $"{cityInfo.Name} ({cityInfo.Uf})"
                    : cityName; // fallback se não encontrar

                var pibScheme = new PibCsvScheme();
                pibScheme.Name = cityName;

                foreach (var yearContext in cityContext.Value)
                {
                    int year = int.Parse(yearContext.Key);

                    foreach (var indexContext in yearContext.Value)
                    {
                        string index = indexContext.Key;
                        string value = indexContext.Value;

                        pibScheme.Index = index;
                        pibScheme.IndexValue = value;

                        pibSchemes.Add(new PibCsvScheme
                        {
                            Name = displayName,
                            Year = year,
                            Index = index, 
                            IndexValue = value
                        });
                    }

                }
            }

            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Agora voce deve selecionar a pasta onde o arquivo CSV será exportado");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para continuar");
            Console.ReadKey();


            string exportPath = Path.Combine(_pathContext.GetPathByInput(), $"{DateTime.Now.Month}-{DateTime.Now.Day}-{DateTime.Now.Hour}-{DateTime.Now.Minute}_consolidado_pib.csv");
            try
            {
                using (var sw = new StreamWriter(exportPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("Cidade;Ano;Indice;Valor");
                    foreach (var scheme in pibSchemes)
                    {
                        sw.WriteLine($"{scheme.Name};{scheme.Year};{scheme.Index};{scheme.IndexValue:D3}");
                    }
                }

                Console.WriteLine("-------------------------------------");
                Console.WriteLine();
                Console.WriteLine($"Relatório consolidado salvo em {exportPath}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("-------------------------------------");
                Console.WriteLine("Erro ao criar arquivo, mesangem de erro abaixo");
                Console.WriteLine();
                Console.WriteLine(ex.Message);
            }
        }
    }
}
