using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DafTools.Model;

internal class CitiesService
{
    private readonly string _filePath = "Resources/municipios.json";

    public IList<CityInfoResult> CitiesInfo { get; private set; } = new List<CityInfoResult>();

    public IList<CityInfoResult>? LoadCities()
    {
        if (!File.Exists(_filePath))
            return null;


        string json = File.ReadAllText(_filePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };


        CitiesInfo = JsonSerializer.Deserialize<List<CityInfoResult>>(json, options);

        return CitiesInfo;
    }

    public void ListAllCities()
    {
        Console.WriteLine("-------------------------------------");
        Console.WriteLine("Todas as cidades - codigos");
        Console.WriteLine();

        var cities = LoadCities();

        if (cities == null || cities.Count == 0)
        {
            Console.WriteLine("Não foi possível obter as cidades");
            return;
        }

        int count = 0;

        foreach(var city in cities)
        {
            Console.WriteLine($"{count} - {city.Name} ({city.Uf})");

            count++;
            Task.Delay(10).Wait();
        }
        Console.WriteLine();
        Console.WriteLine("-------------------------------------");

        return;
    }

    public void SaveCitiesFile()
    {
        if (CitiesInfo == null || CitiesInfo.Count() == 0)
            return;

        string json = JsonSerializer.Serialize(CitiesInfo, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_filePath, json);

        Console.WriteLine("Arquivo Salvo com sucesso");
        Console.WriteLine();
    }

    public void AddNewCity(CityInfoResult cityResult)
    {
        LoadCities();
        CitiesInfo.Add(cityResult);

        Console.WriteLine("Cidade adicionada com sucesso");
        Console.WriteLine();

        SaveCitiesFile(); // garante persistência
    }

    public void Teste()
    {
        string html = File.ReadAllText("C:\\Users\\Usuario\\Documents\\codigosPib.txt"); // o arquivo que você me mandou

        // Regex para capturar value e nome + UF
        var pattern = @"<option value=""(\d+)"">([^<]+)</option>";
        var matches = Regex.Matches(html, pattern);

        var municipios = new Dictionary<string, int>();

        foreach (Match match in matches)
        {
            string codigo = match.Groups[1].Value;
            string nomeRaw = match.Groups[2].Value.Trim();

            // Exemplo: "Entre Rios ( SC ) - 4205175" → "Entre Rios (SC)"
            nomeRaw = Regex.Replace(nomeRaw, @"-?\s*\d+$", ""); // remove o código final
            nomeRaw = Regex.Replace(nomeRaw, @"\s+", " "); // limpa múltiplos espaços
            nomeRaw = nomeRaw.Replace(" ( ", " (").Replace(" )", ")"); // corrige parenteses

            // Extrair UF entre parênteses
            var ufMatch = Regex.Match(nomeRaw, @"\((.*?)\)");
            string uf = ufMatch.Success ? ufMatch.Groups[1].Value.Trim().ToUpperInvariant() : "";

            // Remover acentos e normalizar o nome
            string nomeSemAcento = new string(
                nomeRaw
                    .Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray()
            );

            // Reconstruir o nome no formato "cidade (UF)"
            // Remove o UF antigo para reinserir formatado
            nomeSemAcento = Regex.Replace(nomeSemAcento, @"\(.*?\)", "").Trim();
            string nomeFinal = $"{nomeSemAcento.ToLowerInvariant()} ({uf})";

            municipios[nomeFinal] = int.Parse(codigo);
        }

        // Serializar em JSON formatado
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(municipios, options);
        File.WriteAllText("lista_codigos_pib.json", json, Encoding.UTF8);

        Console.WriteLine("✅ JSON gerado com sucesso!");
    }
}
