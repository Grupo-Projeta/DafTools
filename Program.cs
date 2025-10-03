using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DafTools.Model;
using DafTools.Services;

namespace DafTools
{
    class Program
    {
        private static bool _isRunning = true;
        private static readonly RequestService _requestService = new RequestService();
        private static readonly ExportService _exportService = new ExportService();
        private static readonly CitiesService _citiesService = new CitiesService();

        [STAThread]
        static void Main(string[] args)
        {
            while (_isRunning)
            {
                Console.WriteLine("-------------------------------------");
                Console.WriteLine("O que deseja fazer ?");
                Console.WriteLine();
                Console.WriteLine("0 - Sair");
                Console.WriteLine("1 - Baixar DAF mensal de todas as cidades (2023 à 2025)");
                Console.WriteLine("2 - Converter todas as DAFS Para CSV único");
                Console.WriteLine("3 - Adicionar Municipio à base interna");
                Console.WriteLine("4 - Listar Municipios da base interna");

                Console.WriteLine();
                Console.Write("Digite uma opção: ");

                var inputOption = Console.ReadKey(intercept: true);

                try
                {
                    var option = int.Parse(inputOption.KeyChar.ToString());
                    Console.WriteLine($"Você escolheu: {option}");
                    Console.WriteLine("-------------------------------------");
                    Console.WriteLine();
                    Task.Delay(1000).Wait();

                    switch (option)
                    {
                        case 0: _isRunning = false; break;

                        case 1:
                            _requestService.RequestDafs().GetAwaiter().GetResult(); break;

                        case 2:
                            _exportService.ExportCsv(); break;

                        case 3: AddNewCityToDataBase().GetAwaiter().GetResult(); break;

                        case 4: _citiesService.ListAllCities(); break;
                     }
                    
                    Task.Delay(1000).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);

                    Task.Delay(1000).Wait();
                    break;
                }
            }
        }

        public static async Task AddNewCityToDataBase()
        {
            Console.Write("Digite o nome da cidade que deseja buscar: ");

            string inputName = Console.ReadLine() ?? string.Empty;

            if (string.IsNullOrEmpty(inputName)) return;

            CityCodeResult? requestResult = await _requestService.RequestCityCode(inputName);

            if (requestResult == null) return;

            _citiesService.AddNewCity(requestResult);
        }
    }
}