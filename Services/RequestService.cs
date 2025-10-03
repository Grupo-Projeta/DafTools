using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DafTools.Model;
using DafTools.Utils;

namespace DafTools.Services
{
    public class RequestService
    {
        public async Task RequestDafs()
        {
            PathUtils pathUtils = new PathUtils();

            Task.Delay(1000).Wait();
            Console.WriteLine();
            Console.WriteLine("-------------------------------------");
            Console.WriteLine("Selecione a pasta onde os dados das cidades serão gerados:");
            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para abrir a seleção");
            Console.ReadKey();

            string targetPath = pathUtils.GetPathByInput();

            string url = "https://demonstrativos.api.daf.bb.com.br/v1/demonstrativo/daf/consulta";
            using var client = new HttpClient();

            DateTime inicio = new DateTime(2023, 1, 1);
            DateTime hoje = DateTime.Today;

            var citiesDialog = new CitiesService();
            var citiesWithCode = citiesDialog.LoadCities();

            if (citiesWithCode == null)
                return;

            foreach (var cityWithCode in citiesWithCode)
            {
                string cityFolder = Path.Combine(targetPath, cityWithCode.Key.Replace(" ", "_"));
                Directory.CreateDirectory(cityFolder);

                for (DateTime data = inicio; data <= hoje; data = data.AddMonths(1))
                {
                    int ano = data.Year;
                    int mes = data.Month;

                    DateTime dataFim = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes));

                    var payload = new
                    {
                        codigoBeneficiario = cityWithCode.Value,
                        codigoFundo = 0,
                        dataInicio = data.ToString("dd.MM.yyyy"),
                        dataFim = dataFim.ToString("dd.MM.yyyy")
                    };

                    string json = JsonSerializer.Serialize(payload);
                    var response = await client.PostAsync(url,
                        new StringContent(json, Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        string resposta = await response.Content.ReadAsStringAsync();

                        // Reformatar com identação
                        using var doc = JsonDocument.Parse(resposta);
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string jsonFormatado = JsonSerializer.Serialize(doc, options);

                        string arquivo = Path.Combine(cityFolder, $"{ano}-{mes:D2}.json");
                        await File.WriteAllTextAsync(arquivo, jsonFormatado, Encoding.UTF8);

                        Console.WriteLine($"{cityWithCode.Key} {ano}-{mes:D2} salvo.");
                    }


                }
            }

            Console.WriteLine("Download concluído!");
        }

        public async Task<CityCodeResult?> RequestCityCode(string cityName)
        {
            IList<CityCodeResult> returnedCities = new List<CityCodeResult>();

            string url = "https://demonstrativos.api.daf.bb.com.br/v1/demonstrativo/daf/beneficiario";

            using var client = new HttpClient();

            var payload = new
            {
                nomeBeneficiarioEntrada = cityName.ToUpper(),
            };
            string jsonPayload = JsonSerializer.Serialize(payload);

            var response = await client.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();

                using var document = JsonDocument.Parse(responseContent);
                var jsonContentList = document.RootElement.GetProperty("listaBeneficiario");

                foreach (var contentItem in jsonContentList.EnumerateArray())
                {
                    string name = contentItem.GetProperty("nomeBeneficiarioSaida").GetString() ?? string.Empty;
                    string uf = contentItem.GetProperty("siglaUnidadeFederacaoSaida").GetString() ?? string.Empty;
                    int code = contentItem.GetProperty("codigoBeneficiarioSaida").GetInt32();

                    returnedCities.Add(new CityCodeResult(name.ToLower(), uf.ToUpper(), code));
                }

                int maxTentativas = 5;
                while (maxTentativas-- > 0)
                {


                    Console.WriteLine("-------------------------------------");
                    Console.WriteLine("Cidades encontradas:");

                    int pointer = 1;
                    foreach (var city in returnedCities)
                    {
                        Console.WriteLine($"{pointer}- {city.Name} - {city.Uf}");
                        pointer++;
                    }
                    Console.WriteLine("-------------------------------------");

                    Console.Write("Digite a opção que deseja adicionar ou 0 para cancelar: ");
                    var optionInput = Console.ReadLine();

                    if (int.TryParse(optionInput, out int option))
                    {
                        if (option == 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Operação cancelada pelo usuário.");
                            return null;
                        }
                        if (option > 0 && option <= returnedCities.Count)
                        {
                            var chosen = returnedCities.ElementAtOrDefault(option - 1);
                            if (chosen != null)
                            {
                                Console.WriteLine($"Opção escolhida: {chosen.Name} - {chosen.Uf}");
                                Console.WriteLine();
                                return chosen;
                            }
                        }
                    }

                    else
                    {
                        
                        Console.WriteLine("Opção Invalida");
                        await Task.Delay(1000);
                    }
                }
            }

            Console.WriteLine("Nome não encontrado ou opção inválida");
            await Task.Delay(1000);
            return null;
        }
    }
}
