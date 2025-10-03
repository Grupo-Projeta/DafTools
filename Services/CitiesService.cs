using System.IO;
using System.Text.Json;
using DafTools.Model;

internal class CitiesService
{
    private readonly string _filePath = "Resources/municipios.json";

    public Dictionary<string, int> CitiesWithCode { get; private set; } = new();

    public Dictionary<string, int>? LoadCities()
    {
        if (!File.Exists(_filePath))
            return null;


        string json = File.ReadAllText(_filePath);
        CitiesWithCode = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                               ?? new Dictionary<string, int>();

        return CitiesWithCode;
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

        foreach(var city in cities)
        {
            Console.WriteLine($"{city.Key} - {city.Value}");
            Task.Delay(10).Wait();
        }
        Console.WriteLine();
        Console.WriteLine("-------------------------------------");

        return;
    }

    public void SaveCitiesFile()
    {
        if (CitiesWithCode == null || CitiesWithCode.Count() == 0)
            return;

        string json = JsonSerializer.Serialize(CitiesWithCode, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_filePath, json);

        Console.WriteLine("Arquivo Salvo com sucesso");
        Console.WriteLine();
    }

    public void AddNewCity(CityCodeResult cityResult)
    {
        LoadCities();
        CitiesWithCode.Add(cityResult.Name, cityResult.Code);

        Console.WriteLine("Cidade adicionada com sucesso");
        Console.WriteLine();

        SaveCitiesFile(); // garante persistência
    }
}
