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
        public void ExportCsv()
        {
            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Selecione a pasta onde estão os DAFs:");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para abrir a seleção");
            Console.ReadKey();


            var pathDialog = new PathUtils();

            string DataPath = pathDialog.GetPathByInput();

            string[] citiesPaths = Directory.GetDirectories(DataPath);

            List<Registro> registros = new();

            foreach (string city in citiesPaths)
            {
                string cityName = Path.GetFileName(city);
                string[] cityJsonFiles = Directory.GetFiles(city, "*.json");

                foreach (string jsonFile in cityJsonFiles)
                {
                    string jsonFileName = Path.GetFileNameWithoutExtension(jsonFile);

                    var fileDate = jsonFileName.Split('-');
                    int ano = int.Parse(fileDate[0]);
                    int mes = int.Parse(fileDate[1]);

                    // carregar JSON
                    string conteudo = File.ReadAllText(jsonFile);
                    using var doc = JsonDocument.Parse(conteudo);
                    var ocorrencias = doc.RootElement.GetProperty("quantidadeOcorrencia");

                    var linhas = ocorrencias.EnumerateArray()
                    .Select(x => x.GetProperty("nomeBeneficio").GetString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                    string fundoAtual = null;
                    bool esperandoDebito = false;
                    bool esperandoCredito = false;
                    bool ignorarPrimeira = true;


                    foreach (var linha in linhas)
                    {

                        if (ignorarPrimeira)
                        {
                            ignorarPrimeira = false;
                            continue; // pula a primeira linha (cidade)
                        }

                        // Passo 1: detectar fundo
                        if (fundoAtual == null && Regex.IsMatch(linha, @"^[A-Z]{2,}\s+-"))
                        {
                            fundoAtual = linha;
                            esperandoDebito = true;
                            esperandoCredito = true;
                        }

                        // Passo 2: capturar débito
                        if (esperandoDebito && linha.Contains("DEBITO FUNDO") && fundoAtual != null)
                        {
                            var partes = linha.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var valorDebito = double.Parse(partes.Last().Remove(partes.Last().Count() - 1)).ToString("F2", new CultureInfo("pt-br"));

                            registros.Add(new Registro
                            {
                                Prefeitura = cityName,
                                Ano = ano,
                                Mes = mes,
                                Fundo = fundoAtual,
                                Debito = valorDebito,
                                Credito = "" // será preenchido depois
                            });

                            esperandoDebito = false; // já achou débito desse fundo
                        }

                        // Passo 3: capturar crédito
                        if (esperandoCredito && linha.Contains("CREDITO FUNDO") && fundoAtual != null)
                        {
                            var partes = linha.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var valorCredito = double.Parse(partes.Last().Remove(partes.Last().Count() - 1)).ToString("F2", new CultureInfo("pt-br"));

                            var ultimo = registros.LastOrDefault(r =>
                                r.Prefeitura == cityName &&
                                r.Ano == ano &&
                                r.Mes == mes &&
                                r.Fundo == fundoAtual);

                            if (ultimo != null)
                                ultimo.Credito = valorCredito;
                            else
                                registros.Add(new Registro
                                {
                                    Prefeitura = cityName,
                                    Ano = ano,
                                    Mes = mes,
                                    Fundo = fundoAtual,
                                    Debito = "",
                                    Credito = valorCredito
                                });

                            esperandoCredito = false;

                            fundoAtual = null;
                        }
                    }

                    Console.WriteLine($"{cityName} {ano}-{mes:D2} processado.");
                }
            }

            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Agora voce deve selecionar a pasta onde o arquivo CSV será exportado");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para continuar");
            Console.ReadKey();


            string exportPath = Path.Combine(pathDialog.GetPathByInput(), $"{DateTime.Now.Month}-{DateTime.Now.Day}-{DateTime.Now.Hour}-{DateTime.Now.Minute}_relatorio_consolidado.csv");
            try
            {
                using (var sw = new StreamWriter(exportPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("Prefeitura;Ano;Mês;Fundo;Débito;Crédito");
                    foreach (var r in registros)
                    {
                        sw.WriteLine($"{r.Prefeitura};{r.Ano};{r.Mes:D2};{r.Fundo};{r.Debito};{r.Credito}");
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
