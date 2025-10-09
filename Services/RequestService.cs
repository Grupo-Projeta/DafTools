using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DafTools.Model;
using DafTools.Utils;

namespace DafTools.Services
{
    public class RequestService
    {
        public async Task RequestDafsData()
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
            var citiesInfo = citiesDialog.LoadCities();

            if (citiesInfo == null)
                return;

            foreach (var cityInfo in citiesInfo)
            {
                string cityFolder = Path.Combine(targetPath, cityInfo.Name.Replace(" ", "_"));
                Directory.CreateDirectory(cityFolder);

                for (DateTime data = inicio; data <= hoje; data = data.AddMonths(1))
                {
                    int ano = data.Year;
                    int mes = data.Month;

                    DateTime dataFim = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes));

                    var payload = new
                    {
                        codigoBeneficiario = cityInfo.DafCode,
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

                        string file = Path.Combine(cityFolder, $"{ano}-{mes:D2}.json");
                        await File.WriteAllTextAsync(file, jsonFormatado, Encoding.UTF8);

                        Console.WriteLine($"{cityInfo.Name} ({cityInfo.Uf}) {ano}-{mes:D2} salvo.");
                    }
                }
            }

            Console.WriteLine("Download concluído!");
        }


        public async Task RequestPibData()
        {
            var citiesDialog = new CitiesService();
            var citiesInfo = citiesDialog.LoadCities();

            string url = "https://www.ibge.gov.br/estatisticas/economicas/contas-nacionais/9088-produto-interno-bruto-dos-municipios.html?t=pib-por-municipio&c=";
            using HttpClient httpClient = new HttpClient();

            // Dicionário principal da cidade
            
            var finalJson = new Dictionary<string, object>();

            foreach (var cityInfo in citiesInfo)
            {
                var cityData = new Dictionary<string, Dictionary<string, string>>();

                var response = await httpClient.GetAsync(url + cityInfo.PibCode);

                if (!response.IsSuccessStatusCode)
                    return;

                string html = await response.Content.ReadAsStringAsync();

                var pattern = @"<p class=['""]ind-label['""]>(.*?)<\/p>\s*<p class=['""]ind-value['""]>(.*?)<\/p>";
                var matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string label = WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                    string value = WebUtility.HtmlDecode(match.Groups[2].Value);
                    value = Regex.Replace(value, "<.*?>", "").Trim();

                    // Extrair o ano (padrão [2021])
                    var yearMatch = Regex.Match(value, @"\[(\d{4})\]");
                    string year = yearMatch.Success ? yearMatch.Groups[1].Value : "desconhecido";

                    // Mover o (×1000) do valor para o label
                    var multiplierMatch = Regex.Match(value, @"\(×1000\)");
                    if (multiplierMatch.Success)
                    {
                        label = $"{label} (×1000)";
                        value = value.Replace("(×1000)", "").Trim();
                    }

                    // Limpar o valor de símbolos e lixo HTML
                    value = value
                        .Replace("R$", "")
                        .Trim();

                    // Remover o [2021] do valor
                    value = Regex.Replace(value, @"\[\d{4}\]", "").Trim();

                    // Se o ano ainda não existe no dicionário, cria
                    if (!cityData.ContainsKey(year))
                        cityData[year] = new Dictionary<string, string>();

                    // Adiciona o indicador e seu valor
                    cityData[year][label] = value;
                }

                finalJson.Add(cityInfo.Name, cityData);

                Console.WriteLine($"{cityInfo.Name} ({cityInfo.Uf}) Analisada");
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(finalJson, options);

            // Salvar em arquivo
            string path = "indicaroes_pib.json";
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);

            Console.WriteLine("Indicadores salvos com sucesso");
        }


        public async Task<CityInfoResult?> RequestCityDafCode(string cityName)
        {
            IList<CityInfoResult> returnedCities = new List<CityInfoResult>();

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
                    int dafCode = contentItem.GetProperty("codigoBeneficiarioSaida").GetInt32();

                    returnedCities.Add(new CityInfoResult(name.ToLower(), uf.ToUpper(), dafCode, 0000));
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
